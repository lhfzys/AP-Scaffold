using AP.Contracts.Hardware.DeviceRuntime;
using AP.Plugin.Plc.Omron.Services;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.Drivers;

/// <summary>
/// TP2-欧姆龙：TagDataType → DataTypeEnum 映射测试。
/// </summary>
public class OmronTypedBatchTests
{
    [Theory]
    [InlineData(TagDataType.Bool, "Bool")]
    [InlineData(TagDataType.Int16, "Int16")]
    [InlineData(TagDataType.UInt16, "UInt16")]
    [InlineData(TagDataType.Int32, "Int32")]
    [InlineData(TagDataType.UInt32, "UInt32")]
    [InlineData(TagDataType.Int64, "Int64")]
    [InlineData(TagDataType.UInt64, "UInt64")]
    [InlineData(TagDataType.Float, "Float")]
    [InlineData(TagDataType.Double, "Double")]
    [InlineData(TagDataType.String, "String")]
    [InlineData(TagDataType.ByteArray, "Byte")]
    public void ToDataTypeEnum_AllSupportedTypes_MapCorrectly(TagDataType type, string expected)
    {
        OmronPlcService.ToDataTypeEnum(type).ToString().Should().Be(expected);
    }
}
