// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.Tests.AlwaysEncryptedTests
{
    /// <summary>
    /// Tests for <c>SqlSymmetricKeyCache</c> concurrent access behavior.
    /// </summary>
    public class SqlSymmetricKeyCacheTests
    {
        // How long the simulated "slow" provider takes per key-decryption call (milliseconds).
        private const int ProviderDelayMs = 50;

        // Number of threads that will concurrently request the same key from the cache.
        private const int ConcurrentThreadCount = 20;

        // Maximum wall-clock time (ms) we allow for all concurrent threads to finish.
        // Without serialization the batch completes in ~ProviderDelayMs.
        // With the old global SemaphoreSlim lock the batch would take at least
        // ConcurrentThreadCount * ProviderDelayMs = 1 000 ms, so we set a generous but
        // still meaningful bound well below that.
        private const int MaxAllowedElapsedMs = 800;

        // ── reflection helpers ────────────────────────────────────────────────────

        private static readonly Assembly s_sqlClientAssembly = typeof(SqlConnection).Assembly;

        private static readonly Type s_sqlSymmetricKeyCacheType =
            s_sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlSymmetricKeyCache", throwOnError: true)!;

        private static readonly MethodInfo s_cacheGetInstanceMethod =
            s_sqlSymmetricKeyCacheType.GetMethod("GetInstance", BindingFlags.Static | BindingFlags.NonPublic)!;

        private static readonly MethodInfo s_cacheGetKeyMethod =
            s_sqlSymmetricKeyCacheType.GetMethod("GetKey", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static readonly Type s_sqlEncryptionKeyInfoType =
            s_sqlClientAssembly.GetType("Microsoft.Data.SqlClient.SqlEncryptionKeyInfo", throwOnError: true)!;

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>Creates a <c>SqlEncryptionKeyInfo</c> via reflection.</summary>
        private static object CreateKeyInfo(string providerName, string keyPath, byte[] encryptedKey)
        {
            object keyInfo = Activator.CreateInstance(s_sqlEncryptionKeyInfoType, nonPublic: true)!;
            s_sqlEncryptionKeyInfoType.GetField("keyStoreName")!.SetValue(keyInfo, providerName);
            s_sqlEncryptionKeyInfoType.GetField("keyPath")!.SetValue(keyInfo, keyPath);
            s_sqlEncryptionKeyInfoType.GetField("encryptedKey")!.SetValue(keyInfo, encryptedKey);
            s_sqlEncryptionKeyInfoType.GetField("algorithmName")!.SetValue(keyInfo, "RSA_OAEP");
            return keyInfo;
        }

        // ── tests ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Regression test for GitHub issue: Always Encrypted with AKV provider fails
        /// intermittently under concurrent load.
        ///
        /// Root cause: <c>SqlSymmetricKeyCache.GetKey</c> was using a global
        /// <c>SemaphoreSlim(1,1)</c> with a synchronous <c>Wait()</c> call. All threads
        /// serialised through that semaphore while one thread performed a slow remote
        /// key-store call (e.g. Azure Key Vault). Under concurrency (≥ 20 threads) this
        /// caused thread-pool starvation, transport-level errors, and spurious
        /// "Invalid key store provider name" exceptions.
        ///
        /// Fix: the global semaphore was removed. <c>MemoryCache</c> is inherently
        /// thread-safe for concurrent reads and writes. Multiple threads may compute the
        /// same cache-miss concurrently; the "last write wins" race is benign and was
        /// already documented in the original code comment
        /// ("In case multiple threads reach here at the same time, the first one wins").
        /// </summary>
        [Fact]
        public void GetKey_ConcurrentCacheMiss_DoesNotSerializeProviderCalls()
        {
            // ── Arrange ───────────────────────────────────────────────────────────
            const string providerName = "CONCURRENT_TEST_PROVIDER";
            const string keyPath = "concurrent-test/key-path";
            byte[] encryptedKey = new byte[32];
            new Random(42).NextBytes(encryptedKey);

            var slowProvider = new SlowKeyStoreProvider(ProviderDelayMs);

            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
                Utility.ClearSqlConnectionGlobalProviders();
                SqlConnection.RegisterColumnEncryptionKeyStoreProviders(
                    new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
                    {
                        { providerName, slowProvider }
                    });
            }

            // Use a SqlConnection object that carries the correct DataSource so that
            // SqlSecurityUtility.ThrowIfKeyPathIsNotTrustedForServer doesn't throw.
            // "Data Source=concurrent-test" is not in ColumnEncryptionTrustedMasterKeyPaths
            // so the trusted-key-path check is a no-op.
            SqlConnection conn = new("Data Source=concurrent-test");

            object keyInfo = CreateKeyInfo(providerName, keyPath, encryptedKey);
            object cacheInstance = s_cacheGetInstanceMethod.Invoke(null, null)!;

            Exception[] threadExceptions = new Exception[ConcurrentThreadCount];
            Thread[] threads = new Thread[ConcurrentThreadCount];

            var barrier = new Barrier(ConcurrentThreadCount); // synchronise all threads to start together

            for (int i = 0; i < ConcurrentThreadCount; i++)
            {
                int idx = i;
                threads[i] = new Thread(() =>
                {
                    barrier.SignalAndWait(); // wait until all threads are ready
                    try
                    {
                        s_cacheGetKeyMethod.Invoke(cacheInstance, new object[] { keyInfo, conn, null });
                    }
                    catch (TargetInvocationException ex) when (ex.InnerException is not null)
                    {
                        threadExceptions[idx] = ex.InnerException;
                    }
                    catch (Exception ex)
                    {
                        threadExceptions[idx] = ex;
                    }
                });
            }

            // ── Act ───────────────────────────────────────────────────────────────
            Stopwatch sw = Stopwatch.StartNew();

            foreach (Thread t in threads)
                t.Start();
            foreach (Thread t in threads)
                t.Join();

            sw.Stop();

            // ── Assert ────────────────────────────────────────────────────────────

            // 1. No thread should have encountered an unexpected exception.
            for (int i = 0; i < ConcurrentThreadCount; i++)
                Assert.True(threadExceptions[i] is null,
                    $"Thread {i} threw: {threadExceptions[i]?.Message ?? "(null)"}");

            // 2. All calls must complete within a time budget that rules out full
            //    serialization.  With the old global lock the minimum wall-clock time
            //    would be ConcurrentThreadCount * ProviderDelayMs ≈ 1 000 ms.
            //    Without the lock the batch completes in roughly ProviderDelayMs.
            //    We use 800 ms as a generous upper bound that still catches the
            //    serialized case.
            Assert.True(sw.ElapsedMilliseconds < MaxAllowedElapsedMs,
                $"Concurrent cache calls took {sw.ElapsedMilliseconds} ms, which suggests " +
                $"provider calls are being serialized (limit: {MaxAllowedElapsedMs} ms). " +
                $"Expected parallel execution in ~{ProviderDelayMs} ms.");

            // 3. The provider should have been called at least once.
            Assert.True(slowProvider.CallCount >= 1,
                "Provider DecryptColumnEncryptionKey was never called.");

            // Cleanup
            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
                Utility.ClearSqlConnectionGlobalProviders();
            }
        }

        /// <summary>
        /// Verifies that a key that is already in the cache is served immediately
        /// without calling the key-store provider again.
        /// </summary>
        [Fact]
        public void GetKey_CacheHit_DoesNotCallProvider()
        {
            const string providerName = "CACHE_HIT_TEST_PROVIDER";
            const string keyPath = "cache-hit-test/key-path";
            byte[] encryptedKey = new byte[32];
            new Random(99).NextBytes(encryptedKey);

            var provider = new SlowKeyStoreProvider(ProviderDelayMs);

            lock (Utility.ClearSqlConnectionGlobalProvidersLock)
            {
                Utility.ClearSqlConnectionGlobalProviders();
                SqlConnection.RegisterColumnEncryptionKeyStoreProviders(
                    new Dictionary<string, SqlColumnEncryptionKeyStoreProvider>
                    {
                        { providerName, provider }
                    });
            }

            // Enable caching so the key is stored after the first decryption.
            TimeSpan originalTtl = SqlConnection.ColumnEncryptionKeyCacheTtl;
            SqlConnection.ColumnEncryptionKeyCacheTtl = TimeSpan.FromHours(1);

            try
            {
                SqlConnection conn = new("Data Source=cache-hit-test");
                object keyInfo = CreateKeyInfo(providerName, keyPath, encryptedKey);
                object cacheInstance = s_cacheGetInstanceMethod.Invoke(null, null)!;

                // First call – cache miss; provider is called.
                s_cacheGetKeyMethod.Invoke(cacheInstance, new object[] { keyInfo, conn, null });
                int callsAfterFirst = provider.CallCount;

                // Second call – should be a cache hit; provider must NOT be called again.
                s_cacheGetKeyMethod.Invoke(cacheInstance, new object[] { keyInfo, conn, null });

                Assert.Equal(callsAfterFirst, provider.CallCount);
            }
            finally
            {
                SqlConnection.ColumnEncryptionKeyCacheTtl = originalTtl;
                lock (Utility.ClearSqlConnectionGlobalProvidersLock)
                {
                    Utility.ClearSqlConnectionGlobalProviders();
                }
            }
        }

        // ── helpers ───────────────────────────────────────────────────────────────

        /// <summary>
        /// A key-store provider that simulates a slow remote call (e.g. Azure Key Vault)
        /// by sleeping for a configurable number of milliseconds before returning a
        /// deterministic 32-byte plaintext key.
        /// </summary>
        private sealed class SlowKeyStoreProvider : SqlColumnEncryptionKeyStoreProvider
        {
            private readonly int _delayMs;
            private int _callCount;

            public SlowKeyStoreProvider(int delayMs) => _delayMs = delayMs;

            /// <summary>Number of times <see cref="DecryptColumnEncryptionKey"/> was called.</summary>
            public int CallCount => _callCount;

            public override byte[] DecryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] encryptedColumnEncryptionKey)
            {
                Interlocked.Increment(ref _callCount);
                Thread.Sleep(_delayMs); // simulate network latency to AKV
                // Return a deterministic 32-byte key derived from the encrypted key bytes
                // (any 32-byte array is acceptable as a plaintext column encryption key).
                byte[] plaintextKey = new byte[32];
                Buffer.BlockCopy(encryptedColumnEncryptionKey, 0,
                    plaintextKey, 0,
                    Math.Min(encryptedColumnEncryptionKey.Length, plaintextKey.Length));
                return plaintextKey;
            }

            public override byte[] EncryptColumnEncryptionKey(
                string masterKeyPath,
                string encryptionAlgorithm,
                byte[] columnEncryptionKey)
                => throw new NotSupportedException("Encryption is not used in this test.");
        }
    }
}
