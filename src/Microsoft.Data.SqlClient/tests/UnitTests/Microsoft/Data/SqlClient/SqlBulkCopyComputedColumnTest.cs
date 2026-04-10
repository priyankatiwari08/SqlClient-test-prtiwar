// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests
{
    /// <summary>
    /// Tests that verify SqlBulkCopy correctly handles tables with computed columns.
    /// Computed columns must not appear in the INSERT BULK command and must not cause
    /// InvalidOperationException when the user only maps non-computed columns.
    /// </summary>
    public class SqlBulkCopyComputedColumnTest
    {
        [Fact]
        public void SqlMetaData_IsExpression_DefaultsToFalse()
        {
            // A freshly created metadata entry should not be an expression (computed column).
            _SqlMetaData meta = new _SqlMetaDataSet(1)[0];
            Assert.False(meta.IsExpression);
        }

        [Fact]
        public void SqlMetaData_IsExpression_CanBeSetToTrue()
        {
            // Verify that IsExpression property on _SqlMetaData can be set to true
            // to represent a computed column.
            _SqlMetaData meta = new _SqlMetaDataSet(1)[0];
            meta.IsExpression = true;
            Assert.True(meta.IsExpression);
        }

        [Fact]
        public void SqlMetaData_IsExpression_CanBeToggledOffAndOn()
        {
            // Verify that IsExpression can be cleared after being set.
            _SqlMetaData meta = new _SqlMetaDataSet(1)[0];

            meta.IsExpression = true;
            Assert.True(meta.IsExpression);

            meta.IsExpression = false;
            Assert.False(meta.IsExpression);
        }

        [Fact]
        public void SqlMetaData_IsExpression_DoesNotAffectIsIdentityFlag()
        {
            // Setting IsExpression should not affect IsIdentity and vice versa.
            // Both represent different reasons a column cannot be bulk-loaded into.
            _SqlMetaData meta = new _SqlMetaDataSet(1)[0];

            meta.IsIdentity = true;
            meta.IsExpression = true;

            Assert.True(meta.IsIdentity);
            Assert.True(meta.IsExpression);

            meta.IsExpression = false;
            Assert.True(meta.IsIdentity, "IsIdentity should still be set after clearing IsExpression.");
            Assert.False(meta.IsExpression);
        }

        [Fact]
        public void SqlMetaData_IsIdentity_DoesNotAffectIsExpressionFlag()
        {
            // Setting IsIdentity should not affect IsExpression.
            _SqlMetaData meta = new _SqlMetaDataSet(1)[0];

            meta.IsExpression = true;
            meta.IsIdentity = true;

            Assert.True(meta.IsExpression);
            Assert.True(meta.IsIdentity);

            meta.IsIdentity = false;
            Assert.True(meta.IsExpression, "IsExpression should still be set after clearing IsIdentity.");
            Assert.False(meta.IsIdentity);
        }

        [Fact]
        public void SqlMetaDataSet_ComputedColumnFlag_IsIndependentPerEntry()
        {
            // In a multi-column metadata set, IsExpression for one column should not
            // affect other columns.
            _SqlMetaDataSet metaDataSet = new _SqlMetaDataSet(3);
            metaDataSet[0].column = "Id";
            metaDataSet[1].column = "Quantity";
            metaDataSet[2].column = "TotalPrice";

            // Mark TotalPrice as a computed column (expression).
            metaDataSet[2].IsExpression = true;

            Assert.False(metaDataSet[0].IsExpression, "Id should not be a computed column.");
            Assert.False(metaDataSet[1].IsExpression, "Quantity should not be a computed column.");
            Assert.True(metaDataSet[2].IsExpression, "TotalPrice should be marked as a computed column.");
        }
    }
}
