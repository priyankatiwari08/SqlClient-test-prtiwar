// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using Xunit;

namespace Microsoft.Data.SqlClient.UnitTests;

/// <summary>
/// Tests that null and non-null values assigned to the SqlBuffer round-trip correctly to their CLR and their
/// their SqlTypes representations.
/// </summary>
/// <remarks>
/// Several methods in this class are internal. This is because their parameters are of SqlBuffer.StorageType,
/// which is non-public.
/// </remarks>
public sealed class SqlBufferTest
{
    private readonly SqlBuffer _target = new();

    /// <summary>
    /// Verifies that SetToDateTimeOffset correctly parses the UTC offset from TDS bytes for scales 0-2
    /// (where the time component uses 3 bytes, and the total data length is 8 bytes).
    /// Regression test for: SqlDataReader.GetFieldValue&lt;DateTimeOffset&gt;() returns wrong offset
    /// for datetimeoffset(0) through datetimeoffset(2).
    /// </summary>
    [Theory]
    // scale 0: '2024-01-15 10:30:00 +05:30', UTC = '2024-01-15 05:00:00'
    // time = 18000 s at scale 0 → [0x50, 0x46, 0x00], date = day 738899 → [0x53, 0x46, 0x0B], offset +330 → [0x4A, 0x01]
    [InlineData(0, new byte[] { 0x50, 0x46, 0x00, 0x53, 0x46, 0x0B, 0x4A, 0x01 }, 2024, 1, 15, 10, 30, 0, 0, 5, 30)]
    // scale 1: '2024-01-15 10:30:00.0 +05:30', UTC = '2024-01-15 05:00:00.0'
    // time = 180000 × 0.1s at scale 1 → [0x20, 0xBF, 0x02], date = day 738899 → [0x53, 0x46, 0x0B], offset +330 → [0x4A, 0x01]
    [InlineData(1, new byte[] { 0x20, 0xBF, 0x02, 0x53, 0x46, 0x0B, 0x4A, 0x01 }, 2024, 1, 15, 10, 30, 0, 0, 5, 30)]
    // scale 2: '2024-01-15 10:30:00.00 +05:30', UTC = '2024-01-15 05:00:00.00'
    // time = 1800000 × 0.01s at scale 2 → [0x40, 0x77, 0x1B], date = day 738899 → [0x53, 0x46, 0x0B], offset +330 → [0x4A, 0x01]
    [InlineData(2, new byte[] { 0x40, 0x77, 0x1B, 0x53, 0x46, 0x0B, 0x4A, 0x01 }, 2024, 1, 15, 10, 30, 0, 0, 5, 30)]
    // scale 0, negative offset: '2024-01-15 04:30:00 -05:30' UTC = '2024-01-15 10:00:00'
    // time = 36000 s at scale 0 → [0xA0, 0x8C, 0x00], date = day 738899 → [0x53, 0x46, 0x0B], offset -330 → [0xB6, 0xFE]
    [InlineData(0, new byte[] { 0xA0, 0x8C, 0x00, 0x53, 0x46, 0x0B, 0xB6, 0xFE }, 2024, 1, 15, 4, 30, 0, 0, -5, -30)]
    public void SetToDateTimeOffset_SmallScale_ReturnsCorrectOffset(
        byte scale, byte[] bytes, int year, int month, int day, int hour, int minute, int second, int ms,
        int offsetHours, int offsetMinutes)
    {
        var expectedOffset = new TimeSpan(offsetHours, offsetMinutes, 0);
        var expected = new DateTimeOffset(year, month, day, hour, minute, second, ms, expectedOffset);

        _target.SetToDateTimeOffset(bytes, scale, scale);

        DateTimeOffset actual = _target.DateTimeOffset;
        Assert.Equal(expected, actual);
        Assert.Equal(expectedOffset, actual.Offset);
    }

    /// <summary>
    /// Verifies that SetToDateTimeOffset correctly parses the UTC offset from TDS bytes for scales 5-7
    /// (where the time component uses 5 bytes, and the total data length is 10 bytes).
    /// </summary>
    [Theory]
    // scale 7: '2024-01-15 10:30:00.0000000 +05:30', UTC = '2024-01-15 05:00:00.0000000'
    // time = 180000000000 × 100ns at scale 7 → [0x00, 0xB8, 0x24, 0xA3, 0x00], date = day 738899 → [0x53, 0x46, 0x0B], offset +330 → [0x4A, 0x01]
    [InlineData(7, new byte[] { 0x00, 0xB8, 0x24, 0xA3, 0x00, 0x53, 0x46, 0x0B, 0x4A, 0x01 }, 2024, 1, 15, 10, 30, 0, 0, 5, 30)]
    public void SetToDateTimeOffset_LargeScale_ReturnsCorrectOffset(
        byte scale, byte[] bytes, int year, int month, int day, int hour, int minute, int second, int ms,
        int offsetHours, int offsetMinutes)
    {
        var expectedOffset = new TimeSpan(offsetHours, offsetMinutes, 0);
        var expected = new DateTimeOffset(year, month, day, hour, minute, second, ms, expectedOffset);

        _target.SetToDateTimeOffset(bytes, scale, scale);

        DateTimeOffset actual = _target.DateTimeOffset;
        Assert.Equal(expected, actual);
        Assert.Equal(expectedOffset, actual.Offset);
    }

    /// <summary>
    /// Verifies that if a SqlBuffer is directly assigned the value of SqlGuid.Null, accessing its Guid property
    /// throws a SqlNullValueException.
    /// </summary>
    [Fact]
    public void GuidShouldThrowWhenSqlGuidNullIsSet()
    {
        _target.SqlGuid = SqlGuid.Null;

        Assert.Throws<SqlNullValueException>(() => _target.Guid);
    }

    /// <summary>
    /// Verifies that if a SqlBuffer is set to null of type Guid or SqlGuid, accessing its Guid property throws
    /// a SqlNullValueException.
    /// </summary>
    [Theory]
    [InlineData(SqlBuffer.StorageType.Guid)]
    [InlineData(SqlBuffer.StorageType.SqlGuid)]
    internal void GuidShouldThrowWhenSetToNullOfTypeIsCalled(SqlBuffer.StorageType storageType)
    {
        _target.SetToNullOfType(storageType);

        Assert.Throws<SqlNullValueException>(() => _target.Guid);
    }

    /// <summary>
    /// Verifies that the Guid property round-trips correctly.
    /// </summary>
    [Fact]
    public void GuidShouldReturnWhenGuidIsSet()
    {
        var expected = Guid.NewGuid();
        _target.Guid = expected;

        Assert.Equal(expected, _target.Guid);
    }

    /// <summary>
    /// Verifies that the SqlGuid property round-trips to the Guid property correctly.
    /// </summary>
    [Fact]
    public void GuidShouldReturnExpectedWhenSqlGuidIsSet()
    {
        var expected = Guid.NewGuid();
        _target.SqlGuid = expected;

        Assert.Equal(expected, _target.Guid);
    }

    /// <summary>
    /// Verifies that if a SqlBuffer is set to null of type Guid or SqlGuid, accessing its SqlGuid property returns
    /// SqlGuid.Null.
    /// </summary>
    [Theory]
    [InlineData(SqlBuffer.StorageType.Guid)]
    [InlineData(SqlBuffer.StorageType.SqlGuid)]
    internal void SqlGuidShouldReturnSqlNullWhenSetToNullOfTypeIsCalled(SqlBuffer.StorageType storageType)
    {
        _target.SetToNullOfType(storageType);

        Assert.Equal(SqlGuid.Null, _target.SqlGuid);
    }

    /// <summary>
    /// Verifies that if a SqlBuffer is directly assigned the value of SqlGuid.Null, accessing its SqlGuid property
    /// returns SqlGuid.Null.
    /// </summary>
    [Fact]
    public void SqlGuidShouldReturnSqlGuidNullWhenSqlGuidNullIsSet()
    {
        _target.SqlGuid = SqlGuid.Null;

        Assert.Equal(SqlGuid.Null, _target.SqlGuid);
    }
    
    /// <summary>
    /// Verifies that the Guid property round-trips to the SqlGuid property correctly.
    /// </summary>
    [Fact]
    public void SqlGuidShouldReturnExpectedWhenGuidIsSet()
    {
        var guid = Guid.NewGuid();
        SqlGuid expected = guid;
        _target.Guid = guid;

        Assert.Equal(expected, _target.SqlGuid);
    }

    /// <summary>
    /// Verifies that the SqlGuid property round-trips correctly.
    /// </summary>
    [Fact]
    public void SqlGuidShouldReturnExpectedWhenSqlGuidIsSet()
    {
        SqlGuid expected = Guid.NewGuid();
        _target.SqlGuid = expected;

        Assert.Equal(expected, _target.SqlGuid);
    }

    /// <summary>
    /// Verifies that the Guid property round-trips to the SqlValue property correctly.
    /// </summary>
    [Fact]
    public void SqlValueShouldReturnExpectedWhenGuidIsSet()
    {
        var guid = Guid.NewGuid();
        SqlGuid expected = guid;
        _target.Guid = guid;

        Assert.Equal(expected, _target.SqlValue);
    }

    /// <summary>
    /// Verifies that the SqlGuid property round-trips to the SqlValue property correctly.
    /// </summary>
    [Fact]
    public void SqlValueShouldReturnExpectedWhenSqlGuidIsSet()
    {
        SqlGuid expected = Guid.NewGuid();
        _target.SqlGuid = expected;

        Assert.Equal(expected, _target.SqlValue);
    }
}
