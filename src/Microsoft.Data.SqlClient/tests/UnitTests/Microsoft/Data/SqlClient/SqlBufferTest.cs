// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Data.SqlTypes;
using System.Globalization;
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

    #region Decimal(38,18) precision tests

    /// <summary>
    /// Verifies that GetDecimal correctly returns a decimal(38,18) value when it fits within
    /// .NET decimal's precision (data4=0, direct conversion path).
    /// This covers the scenario reported in the issue where small decimal(38,18) values
    /// should be returned without precision loss.
    /// </summary>
    [Fact]
    public void Decimal_Scale18Precision38_SmallValue_ReturnsCorrectDecimal()
    {
        // "0.000000000000000001" as decimal(38,18): the unscaled integer is 1, so data4=0.
        // This takes the direct decimal(lo, mid, hi, isNeg, scale) path without ConvertToPrecScale.
        // The column precision is set to 38 as it would be for a decimal(38,18) SQL Server column.
        SqlDecimal sqlDec = SqlDecimal.Parse("0.000000000000000001");
        int[] data = sqlDec.Data;

        _target.SetToDecimal(38, 18, true, data.AsSpan());

        decimal result = _target.Decimal;
        Assert.Equal("0.000000000000000001", result.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies that GetDecimal correctly returns a negative decimal(38,18) value when it fits
    /// within .NET decimal's precision.
    /// </summary>
    [Fact]
    public void Decimal_Scale18Precision38_NegativeSmallValue_ReturnsCorrectDecimal()
    {
        SqlDecimal sqlDec = SqlDecimal.Parse("0.000000000000000001");
        int[] data = sqlDec.Data;

        _target.SetToDecimal(38, 18, false, data.AsSpan());

        decimal result = _target.Decimal;
        Assert.Equal("-0.000000000000000001", result.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies that GetDecimal correctly converts a decimal(38,18) value with data4 != 0 (value
    /// exceeds 2^96) but with trailing zeros that allow the value to be represented in .NET decimal.
    /// This exercises the FindTrailingZerosAndPrec → ConvertToPrecScale conversion path.
    /// </summary>
    [Fact]
    public void Decimal_Scale18Precision38_LargeValueWithTrailingZeros_ReturnsCorrectDecimal()
    {
        // "79228162515.000000000000000000" exceeds 2^96 when unscaled, but the 18 trailing zeros
        // allow it to be represented by removing the scale (integer part = 79228162515 fits in .NET decimal).
        SqlDecimal sqlDec = SqlDecimal.Parse("79228162515.000000000000000000");
        int[] data = sqlDec.Data;

        _target.SetToDecimal(38, 18, true, data.AsSpan());

        decimal result = _target.Decimal;
        // "79228162515.000000000000000000" has 18 trailing zeros (zeroCnt=18), precision=11 digits.
        // ConvertToPrecScale(28, 17) is used (newPrec=28, newScale=28-11=17), giving scale=17.
        // The resulting .NET decimal has 17 decimal places, all zeros.
        Assert.Equal("79228162515.00000000000000000", result.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Verifies that GetDecimal throws OverflowException (not silently truncates) when the
    /// decimal(38,18) value has 38 significant digits and cannot be represented in .NET decimal.
    /// This is the exact scenario from the bug report: 12345678901234567890.123456789012345678.
    /// </summary>
    [Fact]
    public void Decimal_Scale18Precision38_HighPrecisionValue_ThrowsOverflowException()
    {
        // The exact value from the issue: 38 significant digits, no trailing zeros.
        // .NET decimal supports only ~28-29 significant digits, so this must throw OverflowException.
        SqlDecimal sqlDec = SqlDecimal.Parse("12345678901234567890.123456789012345678");
        int[] data = sqlDec.Data;

        _target.SetToDecimal(38, 18, true, data.AsSpan());

        // Must throw OverflowException — not silently truncate to fewer digits
        Assert.Throws<OverflowException>(() => _target.Decimal);
    }

    /// <summary>
    /// Verifies that GetDecimal throws OverflowException for a negative decimal(38,18) value
    /// with 38 significant digits that cannot be represented in .NET decimal.
    /// </summary>
    [Fact]
    public void Decimal_Scale18Precision38_NegativeHighPrecisionValue_ThrowsOverflowException()
    {
        SqlDecimal sqlDec = SqlDecimal.Parse("12345678901234567890.123456789012345678");
        int[] data = sqlDec.Data;

        _target.SetToDecimal(38, 18, false, data.AsSpan());

        Assert.Throws<OverflowException>(() => _target.Decimal);
    }

    #endregion
}
