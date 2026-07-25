using AP.Contracts.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

/// <summary>
/// T3.1 设备抽象契约类型的基本行为（record 相等性 / 预留元数据默认值）。
/// </summary>
public class DeviceContractTests
{
    [Fact]
    public void DeviceInfo_OptionalMetadata_DefaultsToNull()
    {
        var info = new DeviceInfo("plc.main", "主 PLC", DeviceType.Plc, "Mitsubishi");

        info.Group.Should().BeNull();
        info.Description.Should().BeNull();
    }

    [Fact]
    public void DeviceInfo_WithMetadata_PreservesValues()
    {
        var info = new DeviceInfo("plc.main", "主 PLC", DeviceType.Plc, "Mitsubishi")
        {
            Group = "一号产线",
            Description = "冲压线主控"
        };

        info.Group.Should().Be("一号产线");
        info.Description.Should().Be("冲压线主控");
    }

    [Fact]
    public void DeviceConnectionTransition_IsValueEqual()
    {
        var timestamp = DateTime.Now;
        var a = new DeviceConnectionTransition(
            DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting, "心跳丢失", timestamp);
        var b = new DeviceConnectionTransition(
            DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting, "心跳丢失", timestamp);

        a.Should().Be(b);
    }
}
