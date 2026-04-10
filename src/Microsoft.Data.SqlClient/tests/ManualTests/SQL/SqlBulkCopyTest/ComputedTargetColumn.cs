// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.ManualTesting.Tests.SqlBulkCopyTests
{
    public class ComputedTargetColumn
    {
        /// <summary>
        /// Validates that SqlBulkCopy succeeds when the destination table has a persisted computed column
        /// and the user only provides explicit column mappings for non-computed columns.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_TableWithComputedColumn_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string destinationTable = DataTestUtility.GetShortName("ComputedColumn");

            using SqlConnection dstConn = new(connectionString);
            using SqlCommand dstCmd = dstConn.CreateCommand();

            dstConn.Open();

            try
            {
                DataTestUtility.CreateTable(dstConn, destinationTable, """
(
    Id INT IDENTITY PRIMARY KEY,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    TotalPrice AS (CAST(Quantity AS DECIMAL(10,2)) * UnitPrice) PERSISTED
)
""");

                DataTable dataTable = new();
                dataTable.Columns.Add("Quantity", typeof(int));
                dataTable.Columns.Add("UnitPrice", typeof(decimal));
                dataTable.Rows.Add(5, 19.99m);
                dataTable.Rows.Add(3, 9.50m);

                using SqlBulkCopy bulkCopy = new(dstConn);
                bulkCopy.DestinationTableName = destinationTable;
                bulkCopy.ColumnMappings.Add("Quantity", "Quantity");
                bulkCopy.ColumnMappings.Add("UnitPrice", "UnitPrice");

                // Should not throw InvalidOperationException
                bulkCopy.WriteToServer(dataTable);

                // Verify data was inserted correctly
                using SqlCommand selectCmd = new($"SELECT COUNT(*) FROM {destinationTable}", dstConn);
                int count = (int)selectCmd.ExecuteScalar();
                Assert.Equal(2, count);

                // Verify the computed column was calculated correctly
                using SqlCommand verifyCmd = new($"SELECT Quantity, UnitPrice, TotalPrice FROM {destinationTable} ORDER BY Quantity", dstConn);
                using SqlDataReader reader = verifyCmd.ExecuteReader();

                Assert.True(reader.Read());
                Assert.Equal(3, reader.GetInt32(0));
                Assert.Equal(9.50m, reader.GetDecimal(1));
                Assert.Equal(28.50m, reader.GetDecimal(2));  // 3 * 9.50

                Assert.True(reader.Read());
                Assert.Equal(5, reader.GetInt32(0));
                Assert.Equal(19.99m, reader.GetDecimal(1));
                Assert.Equal(99.95m, reader.GetDecimal(2));  // 5 * 19.99
            }
            finally
            {
                DataTestUtility.DropTable(dstConn, destinationTable);
            }
        }

        /// <summary>
        /// Validates that SqlBulkCopy succeeds asynchronously when the destination table has
        /// a persisted computed column and the user only provides explicit column mappings
        /// for non-computed columns.
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public async Task WriteToServerAsync_TableWithComputedColumn_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string destinationTable = DataTestUtility.GetShortName("ComputedColumnAsync");

            using SqlConnection dstConn = new(connectionString);
            using SqlCommand dstCmd = dstConn.CreateCommand();

            await dstConn.OpenAsync();

            try
            {
                DataTestUtility.CreateTable(dstConn, destinationTable, """
(
    Id INT IDENTITY PRIMARY KEY,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    TotalPrice AS (CAST(Quantity AS DECIMAL(10,2)) * UnitPrice) PERSISTED
)
""");

                DataTable dataTable = new();
                dataTable.Columns.Add("Quantity", typeof(int));
                dataTable.Columns.Add("UnitPrice", typeof(decimal));
                dataTable.Rows.Add(5, 19.99m);
                dataTable.Rows.Add(3, 9.50m);

                using SqlBulkCopy bulkCopy = new(dstConn);
                bulkCopy.DestinationTableName = destinationTable;
                bulkCopy.ColumnMappings.Add("Quantity", "Quantity");
                bulkCopy.ColumnMappings.Add("UnitPrice", "UnitPrice");

                // Should not throw InvalidOperationException
                await bulkCopy.WriteToServerAsync(dataTable);

                // Verify data was inserted correctly
                using SqlCommand selectCmd = new($"SELECT COUNT(*) FROM {destinationTable}", dstConn);
                int count = (int)await selectCmd.ExecuteScalarAsync();
                Assert.Equal(2, count);
            }
            finally
            {
                DataTestUtility.DropTable(dstConn, destinationTable);
            }
        }

        /// <summary>
        /// Validates that SqlBulkCopy works when the computed column appears before other columns
        /// in the table definition (i.e., when the computed column is not the last column).
        /// </summary>
        [ConditionalFact(typeof(DataTestUtility), nameof(DataTestUtility.AreConnStringsSetup), nameof(DataTestUtility.IsNotAzureSynapse))]
        public void WriteToServer_ComputedColumnInMiddle_Succeeds()
        {
            string connectionString = DataTestUtility.TCPConnectionString;
            string destinationTable = DataTestUtility.GetShortName("ComputedColMiddle");

            using SqlConnection dstConn = new(connectionString);
            using SqlCommand dstCmd = dstConn.CreateCommand();

            dstConn.Open();

            try
            {
                // The computed column appears before other regular columns (TotalPrice between UnitPrice and Description)
                DataTestUtility.CreateTable(dstConn, destinationTable, """
(
    Id INT IDENTITY PRIMARY KEY,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    TotalPrice AS (CAST(Quantity AS DECIMAL(10,2)) * UnitPrice) PERSISTED,
    Description NVARCHAR(100) NULL
)
""");

                DataTable dataTable = new();
                dataTable.Columns.Add("Quantity", typeof(int));
                dataTable.Columns.Add("UnitPrice", typeof(decimal));
                dataTable.Columns.Add("Description", typeof(string));
                dataTable.Rows.Add(5, 19.99m, "Product A");

                using SqlBulkCopy bulkCopy = new(dstConn);
                bulkCopy.DestinationTableName = destinationTable;
                bulkCopy.ColumnMappings.Add("Quantity", "Quantity");
                bulkCopy.ColumnMappings.Add("UnitPrice", "UnitPrice");
                bulkCopy.ColumnMappings.Add("Description", "Description");

                // Should not throw InvalidOperationException even though TotalPrice is in the middle
                bulkCopy.WriteToServer(dataTable);

                // Verify data was inserted correctly
                using SqlCommand verifyCmd = new($"SELECT Quantity, UnitPrice, TotalPrice, Description FROM {destinationTable}", dstConn);
                using SqlDataReader reader = verifyCmd.ExecuteReader();

                Assert.True(reader.Read());
                Assert.Equal(5, reader.GetInt32(0));
                Assert.Equal(19.99m, reader.GetDecimal(1));
                Assert.Equal(99.95m, reader.GetDecimal(2));  // 5 * 19.99
                Assert.Equal("Product A", reader.GetString(3));
            }
            finally
            {
                DataTestUtility.DropTable(dstConn, destinationTable);
            }
        }
    }
}
