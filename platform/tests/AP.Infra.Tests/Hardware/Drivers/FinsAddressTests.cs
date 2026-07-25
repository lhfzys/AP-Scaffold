using AP.Plugin.Plc.Omron.Addressing;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.Drivers;

/// <summary>
/// T2.3 欧姆龙 FINS 地址领域对象测试：解析 / 规范化 / 结构化错误 / 值相等。
/// </summary>
public class FinsAddressTests
{
    [Theory]
    [InlineData("D100", "D", 0, 100, null)]
    [InlineData("D100.0", "D", 0, 100, (byte)0)]
    [InlineData("D100.15", "D", 0, 100, (byte)15)]
    [InlineData("C0.0", "C", 0, 0, (byte)0)]
    [InlineData("W10", "W", 0, 10, null)]
    [InlineData("H5.3", "H", 0, 5, (byte)3)]
    [InlineData("A99", "A", 0, 99, null)]
    [InlineData("E0.100", "E", 0, 100, null)]      // E 区带存储体号
    [InlineData("E1.100.0", "E", 1, 100, (byte)0)] // E 区位地址
    public void TryParse_ValidAddress_ParsesParts(string raw, string area, int bank, int offset, byte? bit)
    {
        var ok = FinsAddress.TryParse(raw, out var address, out var error, out _);

        ok.Should().BeTrue();
        error.Should().Be(FinsAddressParseError.None);
        address.Area.ToString().Should().Be(area);
        address.Bank.Should().Be(bank);
        address.Offset.Should().Be(offset);
        address.Bit.Should().Be(bit);
    }

    [Theory]
    [InlineData("d100", "D100")]          // 小写大写化
    [InlineData(" D100 ", "D100")]        // 去首尾空白
    [InlineData("D0100", "D100")]         // 去前导零
    [InlineData("d100.05", "D100.5")]     // 位号去前导零
    [InlineData("e0.0100", "E0.100")]     // E 区偏移去前导零
    public void Normalize_VariousForms_ProduceSingleCanonicalForm(string raw, string expected)
    {
        FinsAddress.Parse(raw).Normalized.Should().Be(expected);
    }

    [Fact]
    public void SameAddress_DifferentForms_AreEqual()
    {
        var a = FinsAddress.Parse("D0100.05");
        var b = FinsAddress.Parse("d100.5");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Theory]
    [InlineData(null, "Empty")]
    [InlineData("", "Empty")]
    [InlineData("   ", "Empty")]
    [InlineData("M100", "UnknownArea")]        // M 不是 FINS 区
    [InlineData("100", "UnknownArea")]         // 数字开头
    [InlineData("E.100", "InvalidBankNumber")] // E 区缺体号
    [InlineData("E16.100", "InvalidBankNumber")] // 体号超 0-F
    [InlineData("EX.100", "InvalidBankNumber")]  // 体号非数字
    [InlineData("E0", "InvalidBankNumber")]    // E 区缺偏移段
    [InlineData("D", "InvalidOffset")]         // 缺偏移
    [InlineData("D10A", "InvalidOffset")]      // 偏移含字母
    [InlineData("D100.16", "InvalidBitPosition")] // 位号 > 15
    [InlineData("D100.-1", "InvalidBitPosition")]
    [InlineData("D100.0.0", "InvalidBitPosition")]  // 段过多
    [InlineData("D65536", "OffsetOutOfRange")]      // 超 MaxOffset
    public void TryParse_InvalidAddress_ReturnsStructuredError(string? raw, string expected)
    {
        var ok = FinsAddress.TryParse(raw, out _, out var error, out var message);

        ok.Should().BeFalse();
        error.ToString().Should().Be(expected);
        message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_InvalidAddress_ThrowsWithErrorCode()
    {
        var act = () => FinsAddress.Parse("D100.16");

        act.Should().Throw<FinsAddressException>()
            .Which.Error.ToString().Should().Be("InvalidBitPosition");
    }

    [Fact]
    public void Parse_InvalidAddress_ExceptionIsArgumentException()
    {
        // 符合 ERROR_HANDLING.md：地址非法属调用方编程错误，为 ArgumentException 子类
        var act = () => FinsAddress.Parse("M100");

        act.Should().Throw<ArgumentException>();
    }
}
