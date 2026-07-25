using AP.Plugin.Plc.Siemens.Addressing;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.Drivers;

/// <summary>
/// T2.2 西门子 S7 地址领域对象测试：解析 / 规范化 / 结构化错误 / 值相等。
/// </summary>
public class S7AddressTests
{
    [Theory]
    [InlineData("M0.0", "M", 0, 0, (byte)0)]
    [InlineData("I0.1", "I", 0, 0, (byte)1)]
    [InlineData("Q2.7", "Q", 0, 2, (byte)7)]
    [InlineData("M10", "M", 0, 10, null)]          // 无位号（字节/字访问）
    [InlineData("DB1.0.0", "DB", 1, 0, (byte)0)]
    [InlineData("DB2.100.5", "DB", 2, 100, (byte)5)]
    [InlineData("DB1.2", "DB", 1, 2, null)]        // DB 无位号
    public void TryParse_ValidAddress_ParsesParts(string raw, string area, int dbNumber, int offset, byte? bit)
    {
        var ok = S7Address.TryParse(raw, out var address, out var error, out _);

        ok.Should().BeTrue();
        error.Should().Be(S7AddressParseError.None);
        address.Area.ToString().Should().Be(area);
        address.DbNumber.Should().Be(dbNumber);
        address.Offset.Should().Be(offset);
        address.Bit.Should().Be(bit);
    }

    [Theory]
    [InlineData("db1.0.0", "DB1.0.0")]   // 小写大写化
    [InlineData(" DB1.0.0 ", "DB1.0.0")] // 去首尾空白
    [InlineData("DB01.02.0", "DB1.2.0")] // 去前导零
    [InlineData("m0.0", "M0.0")]
    [InlineData("M010", "M10")]
    public void Normalize_VariousForms_ProduceSingleCanonicalForm(string raw, string expected)
    {
        S7Address.Parse(raw).Normalized.Should().Be(expected);
    }

    [Fact]
    public void SameAddress_DifferentForms_AreEqual()
    {
        var a = S7Address.Parse("DB01.02.0");
        var b = S7Address.Parse("db1.2.0");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Theory]
    [InlineData(null, "Empty")]
    [InlineData("", "Empty")]
    [InlineData("   ", "Empty")]
    [InlineData("V100", "UnknownArea")]       // 不支持的区
    [InlineData("D100", "UnknownArea")]       // D 不是 S7 区（DB 才是）
    [InlineData("100", "UnknownArea")]        // 数字开头
    [InlineData("DB.0.0", "InvalidDbNumber")] // 缺 DB 号
    [InlineData("DB0.0.0", "InvalidDbNumber")]// DB 号必须 >= 1
    [InlineData("DBX.0.0", "InvalidDbNumber")]// DB 号非数字
    [InlineData("DB1", "InvalidDbNumber")]    // DB 区缺偏移段（parts<2 → 按 DB 号非法报出）
    [InlineData("M", "InvalidOffset")]        // 缺偏移
    [InlineData("M1A", "InvalidOffset")]      // 偏移含字母
    [InlineData("DB1.A.0", "InvalidOffset")]  // DB 偏移含字母
    [InlineData("M0.8", "InvalidBitPosition")]// 位号 > 7
    [InlineData("M0.-1", "InvalidBitPosition")]
    [InlineData("DB1.0.0.0", "InvalidBitPosition")] // 段过多
    [InlineData("M65536", "OffsetOutOfRange")]      // 超 MaxOffset
    public void TryParse_InvalidAddress_ReturnsStructuredError(string? raw, string expected)
    {
        var ok = S7Address.TryParse(raw, out _, out var error, out var message);

        ok.Should().BeFalse();
        error.ToString().Should().Be(expected);
        message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_InvalidAddress_ThrowsWithErrorCode()
    {
        var act = () => S7Address.Parse("M0.8");

        act.Should().Throw<S7AddressException>()
            .Which.Error.ToString().Should().Be("InvalidBitPosition");
    }

    [Fact]
    public void Parse_InvalidAddress_ExceptionIsArgumentException()
    {
        // 符合 ERROR_HANDLING.md：地址非法属调用方编程错误，为 ArgumentException 子类
        var act = () => S7Address.Parse("V100");

        act.Should().Throw<ArgumentException>();
    }
}
