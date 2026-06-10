// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    public class SqlBulkCopyAsyncCancellationTest
    {
        private static readonly MethodInfo s_prepareAsyncBulkCopyCancellation =
            typeof(SqlBulkCopy).GetMethod("PrepareAsyncBulkCopyCancellation", BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static CancellationToken InvokePrepareAsyncBulkCopyCancellation(
            SqlBulkCopy bulkCopy,
            TaskCompletionSource<object> completion,
            CancellationToken cancellationToken)
        {
            Assert.NotNull(s_prepareAsyncBulkCopyCancellation);

            return (CancellationToken)s_prepareAsyncBulkCopyCancellation.Invoke(bulkCopy, new object[] { completion, cancellationToken })!;
        }

        [Fact]
        public async Task PrepareAsyncBulkCopyCancellation_UserCancellation_CancelsCompletion()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.Default, null);
            TaskCompletionSource<object> completion = new();
            using CancellationTokenSource cancellationTokenSource = new();

            CancellationToken effectiveToken = InvokePrepareAsyncBulkCopyCancellation(bulkCopy, completion, cancellationTokenSource.Token);

            Assert.True(effectiveToken.CanBeCanceled);

            cancellationTokenSource.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(completion.Task.IsCanceled);
        }

        [Fact]
        public async Task PrepareAsyncBulkCopyCancellation_Timeout_FaultsCompletion()
        {
            using SqlBulkCopy bulkCopy = new(new SqlConnection(), SqlBulkCopyOptions.Default, null)
            {
                BulkCopyTimeout = 1
            };
            TaskCompletionSource<object> completion = new();

            CancellationToken effectiveToken = InvokePrepareAsyncBulkCopyCancellation(bulkCopy, completion, CancellationToken.None);

            Assert.True(effectiveToken.CanBeCanceled);

            SqlException exception = await Assert.ThrowsAsync<SqlException>(
                async () => await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)));

            Assert.False(completion.Task.IsCanceled);
            Assert.NotNull(exception);
        }
    }
}
