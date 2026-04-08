// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading.Tasks;
using Microsoft.SqlServer.TDS;
using Microsoft.SqlServer.TDS.ColMetadata;
using Microsoft.SqlServer.TDS.Done;
using Microsoft.SqlServer.TDS.EndPoint;
using Microsoft.SqlServer.TDS.Row;
using Microsoft.SqlServer.TDS.SQLBatch;
using Microsoft.SqlServer.TDS.Servers;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests.SimulatedServerTests
{
    /// <summary>
    /// Tests that <see cref="SqlDataReader.GetFieldValue{T}"/> and
    /// <see cref="SqlDataReader.GetFieldValueAsync{T}"/> return correct results for
    /// nullable value type parameters (e.g. <c>int?</c>, <c>bool?</c>, etc.)
    /// both when the column is non-null and when it is null (SQL NULL).
    /// </summary>
    public class SqlDataReaderNullableGetFieldValueTests
    {
        private const string SelectIntValueQuery = "select int_value";
        private const string SelectNullIntValueQuery = "select null_int_value";

        private static SqlConnectionStringBuilder BuildConnectionString(TdsServer server)
            => new SqlConnectionStringBuilder()
            {
                DataSource = $"localhost,{server.EndPoint.Port}",
                Encrypt = SqlConnectionEncryptOption.Optional,
            };

        // ----------------------------------------------------------------
        // Non-null value tests
        // ----------------------------------------------------------------

        [Fact]
        public void GetFieldValue_NullableInt_NonNullColumn_ReturnsValue()
        {
            using TdsServer server = new NullableIntColumnTdsServer(value: 42);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server).ConnectionString);
            connection.Open();
            using SqlCommand command = new(SelectIntValueQuery, connection);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read());

            int? result = reader.GetFieldValue<int?>(0);

            Assert.Equal(42, result);
        }

        [Fact]
        public async Task GetFieldValueAsync_NullableInt_NonNullColumn_ReturnsValue()
        {
            using TdsServer server = new NullableIntColumnTdsServer(value: 42);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server).ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = new(SelectIntValueQuery, connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            int? result = await reader.GetFieldValueAsync<int?>(0);

            Assert.Equal(42, result);
        }

        // ----------------------------------------------------------------
        // Null value tests
        // ----------------------------------------------------------------

        [Fact]
        public void GetFieldValue_NullableInt_NullColumn_ReturnsNull()
        {
            using TdsServer server = new NullableIntColumnTdsServer(value: null);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server).ConnectionString);
            connection.Open();
            using SqlCommand command = new(SelectNullIntValueQuery, connection);
            using SqlDataReader reader = command.ExecuteReader();
            Assert.True(reader.Read());

            int? result = reader.GetFieldValue<int?>(0);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetFieldValueAsync_NullableInt_NullColumn_ReturnsNull()
        {
            using TdsServer server = new NullableIntColumnTdsServer(value: null);
            server.Start();

            using SqlConnection connection = new(BuildConnectionString(server).ConnectionString);
            await connection.OpenAsync();
            using SqlCommand command = new(SelectNullIntValueQuery, connection);
            using SqlDataReader reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            int? result = await reader.GetFieldValueAsync<int?>(0);

            Assert.Null(result);
        }

        // ----------------------------------------------------------------
        // Private helper: custom TDS server that returns a single int column
        // ----------------------------------------------------------------

        /// <summary>
        /// A minimal TDS server that responds to <see cref="SelectIntValueQuery"/> and
        /// <see cref="SelectNullIntValueQuery"/> with a single nullable-int column row.
        /// </summary>
        private sealed class NullableIntColumnTdsServer : TdsServer
        {
            /// <summary>Byte length for an IntN (INT) column: 4 bytes.</summary>
            private const byte IntNLengthBytes = 4;

            private readonly int? _value;

            public NullableIntColumnTdsServer(int? value)
                : base(new TdsServerArguments())
            {
                _value = value;
            }

            public override TDSMessageCollection OnSQLBatchRequest(ITDSServerSession session, TDSMessage message)
            {
                TDSSQLBatchToken batchRequest = message[0] as TDSSQLBatchToken;
                string text = batchRequest?.Text ?? string.Empty;

                if (text.Contains(SelectIntValueQuery, StringComparison.OrdinalIgnoreCase) ||
                    text.Contains(SelectNullIntValueQuery, StringComparison.OrdinalIgnoreCase))
                {
                    return BuildIntResponse();
                }

                return base.OnSQLBatchRequest(session, message);
            }

            private TDSMessageCollection BuildIntResponse()
            {
                // Column metadata – a nullable INT column
                TDSColMetadataToken metadata = new TDSColMetadataToken();
                TDSColumnData column = new TDSColumnData();
                column.DataType = TDSDataType.IntN;
                column.DataTypeSpecific = IntNLengthBytes;
                column.Flags.IsNullable = true;
                column.Flags.Updatable = TDSColumnDataUpdatableFlag.ReadOnly;
                metadata.Columns.Add(column);

                // Data row
                TDSRowToken row = new TDSRowToken(metadata);
                // Passing null serialises the value as SQL NULL; passing int serialises normally.
                row.Data.Add(_value.HasValue ? (object)_value.Value : null);

                TDSDoneToken done = new TDSDoneToken(
                    TDSDoneTokenStatusType.Final | TDSDoneTokenStatusType.Count,
                    TDSDoneTokenCommandType.Select,
                    1);

                TDSMessage responseMessage = new TDSMessage(TDSMessageType.Response, metadata, row, done);
                return new TDSMessageCollection(responseMessage);
            }
        }
    }
}
