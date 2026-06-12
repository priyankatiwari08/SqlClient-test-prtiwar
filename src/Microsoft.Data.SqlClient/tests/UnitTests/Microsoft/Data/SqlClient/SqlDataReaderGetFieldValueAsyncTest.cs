// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Data;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

public class SqlDataReaderGetFieldValueAsyncTest
{
    [Fact]
    public async Task GetFieldValueAsync_NullableInt_ReturnsNullForDbNull()
    {
        SqlDataReader reader = new(command: null, behavior: CommandBehavior.Default);
        SqlBuffer[] data = [new SqlBuffer()];
        data[0].SetToNullOfType(SqlBuffer.StorageType.Int32);

        _SqlMetaDataSet metaData = new(1);
        metaData[0].type = SqlDbType.Int;
        metaData[0].metaType = MetaType.GetMetaTypeFromSqlDbType(SqlDbType.Int, isMultiValued: false);

        SetField(reader, "_data", data);
        SetField(reader, "_metaData", metaData);
        reader._sharedState._dataReady = true;
        reader._sharedState._nextColumnDataToRead = 1;

        int? value = await reader.GetFieldValueAsync<int?>(0);

        Assert.Null(value);
    }

    private static void SetField(object target, string fieldName, object value)
    {
        FieldInfo? field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        field!.SetValue(target, value);
    }
}
