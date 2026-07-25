using AP.Plugin.Plc.Mitsubishi.Addressing;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.Drivers;

/// <summary>
/// T2.1 三菱 MC 地址领域对象测试：解析 / 规范化 / 结构化错误 / 值相等。
/// </summary>
public class McAddressTests
{
    [Theory]
    [InlineData("D100", "D", 100, null)]
    [InlineData("M0", "M", 0, null)]
    [InlineData("X1A", "X", 0x1A, null)]   // X 为十六进制偏移
    [InlineData("YFF", "Y", 0xFF, null)]
    [InlineData("B2B", "B", 0x2B, null)]
    [InlineData("W1F", "W", 0x1F, null)]
    [InlineData("ZR999", "ZR", 999, null)]  // 两字母区
    [InlineData("SM400", "SM", 400, null)]
    [InlineData("SD200", "SD", 200, null)]
    [InlineData("L5", "L", 5, null)]
    [InlineData("F10", "F", 10, null)]
    [InlineData("V3", "V", 3, null)]
    [InlineData("S7", "S", 7, null)]
    [InlineData("R88", "R", 88, null)]
    public void TryParse_ValidAddress_ParsesAreaAndOffset(string raw, string area, int offset, byte? bit)
    {
        var ok = McAddress.TryParse(raw, out var address, out var error, out _);

        ok.Should().BeTrue();
        error.Should().Be(AddressParseError.None);
        address.Area.ToString().Should().Be(area);
        address.Offset.Should().Be(offset);
        address.Bit.Should().Be(bit);
    }

    [Fact]
    public void TryParse_BitAddress_ParsesBit()
    {
        var ok = McAddress.TryParse("D100.F", out var address, out _, out _);

        ok.Should().BeTrue();
        address.IsBitAddress.Should().BeTrue();
        address.Bit.Should().Be((byte)0xF);
    }

    [Theory]
    [InlineData("d100", "D100")]          // 小写区域大写化
    [InlineData(" D100 ", "D100")]        // 去首尾空白
    [InlineData("D0100", "D100")]         // 去前导零
    [InlineData("x0a", "XA")]             // 十六进制区小写转大写 + 去前导零
    [InlineData("D100.f", "D100.F")]      // 位号大写
    [InlineData("zr0010", "ZR10")]        // 两字母区规范化
    public void Normalize_VariousForms_ProduceSingleCanonicalForm(string raw, string expected)
    {
        McAddress.Parse(raw).Normalized.Should().Be(expected);
    }

    [Fact]
    public void SameAddress_DifferentForms_AreEqual()
    {
        var a = McAddress.Parse("D0100");
        var b = McAddress.Parse("d100");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Theory]
    [InlineData(null, "Empty")]
    [InlineData("", "Empty")]
    [InlineData("   ", "Empty")]
    [InlineData("Q100", "UnknownArea")]   // 不存在的区
    [InlineData("Z100", "UnknownArea")]   // Z 不是区（ZR 才是）
    [InlineData("100", "UnknownArea")]    // 数字开头
    [InlineData("D", "InvalidOffset")]    // 缺偏移
    [InlineData("D10A", "InvalidOffset")] // 十进制区含字母
    [InlineData("M100.0", "InvalidBitPosition")] // 位元件区带位号
    [InlineData("D100.G", "InvalidBitPosition")] // 位号非 0-F
    [InlineData("D100.12", "InvalidBitPosition")] // 位号超一位
    [InlineData("D16777216", "OffsetOutOfRange")] // 超 MaxOffset
    public void TryParse_InvalidAddress_ReturnsStructuredError(string? raw, string expected)
    {
        var ok = McAddress.TryParse(raw, out _, out var error, out var message);

        ok.Should().BeFalse();
        error.ToString().Should().Be(expected);
        message.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Parse_InvalidAddress_ThrowsWithErrorCode()
    {
        var act = () => McAddress.Parse("D10A");

        act.Should().Throw<MitsubishiAddressException>()
            .Which.Error.ToString().Should().Be("InvalidOffset");
    }

    [Fact]
    public void Parse_InvalidAddress_ExceptionIsArgumentException()
    {
        // 符合 ERROR_HANDLING.md：地址非法属调用方编程错误，为 ArgumentException 子类
        var act = () => McAddress.Parse("Q100");

        act.Should().Throw<ArgumentException>();
    }
}
