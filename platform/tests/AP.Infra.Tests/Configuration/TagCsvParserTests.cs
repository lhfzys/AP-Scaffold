using AP.Plugin.TagConfiguration.Services;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Configuration;

public class TagCsvParserTests
{
    [Fact]
    public void Parse_EnglishHeader_ReturnsRows()
    {
        var csv = """
            Name,DeviceId,Address,DataType,Access,Group,Unit,Description,IntervalMs
            Temp,plc.main,DB1.2,Int16,ReadOnly,Line1,℃,炉温,500
            Start,plc.main,DB1.0.0,Bool,ReadWrite,,,启动,
            """;

        var result = TagCsvParser.Parse(csv);

        result.Success.Should().BeTrue(string.Join("; ", result.Errors));
        result.Rows.Should().HaveCount(2);
        result.Rows[0].Name.Should().Be("Temp");
        result.Rows[0].DataType.Should().Be(AP.Contracts.Hardware.DeviceRuntime.TagDataType.Int16);
        result.Rows[0].IntervalOverrideMs.Should().Be(500);
        result.Rows[1].Access.Should().Be(AP.Contracts.Hardware.DeviceRuntime.TagAccess.ReadWrite);
    }

    [Fact]
    public void Parse_ChineseHeader_AndQuotedField_AreSupported()
    {
        var csv = """
            点名,设备,地址,类型,读写,描述
            Temp,plc.main,DB1.2,Int16,ReadOnly,"炉温,1号炉"
            """;

        var result = TagCsvParser.Parse(csv);

        result.Success.Should().BeTrue(string.Join("; ", result.Errors));
        result.Rows.Should().ContainSingle();
        result.Rows[0].Description.Should().Be("炉温,1号炉");
    }

    [Fact]
    public void Parse_DuplicateNames_ReturnsError()
    {
        var csv = """
            Name,DeviceId,Address
            Temp,plc.main,DB1.2
            temp,plc.main,DB1.4
            """;

        var result = TagCsvParser.Parse(csv);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("重复"));
    }

    [Fact]
    public void Parse_MissingRequiredHeader_ReturnsError()
    {
        var result = TagCsvParser.Parse("Name,Address\nTemp,DB1.2");

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("表头"));
    }
}
