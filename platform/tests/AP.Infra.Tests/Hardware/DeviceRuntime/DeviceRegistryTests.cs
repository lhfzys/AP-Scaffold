using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class DeviceRegistryTests
{
    [Fact]
    public void Register_ThenGet_ReturnsDevice()
    {
        var registry = new DeviceRegistry();
        var device = CreateDevice("plc.main");

        registry.Register(device);

        registry.Get("plc.main").Should().Be(device);
    }

    [Fact]
    public void Find_CaseInsensitive_ReturnsDevice()
    {
        var registry = new DeviceRegistry();
        var device = CreateDevice("plc.main");
        registry.Register(device);

        registry.Find("PLC.MAIN").Should().Be(device);
        registry.Find("plc.Main").Should().Be(device);
    }

    [Fact]
    public void Find_Unregistered_ReturnsNull()
    {
        var registry = new DeviceRegistry();

        registry.Find("ghost").Should().BeNull();
    }

    [Fact]
    public void Get_Unregistered_ThrowsKeyNotFoundException()
    {
        var registry = new DeviceRegistry();

        var act = () => registry.Get("ghost");

        act.Should().Throw<KeyNotFoundException>().WithMessage("*ghost*");
    }

    [Fact]
    public void Register_DuplicateId_ThrowsArgumentException()
    {
        var registry = new DeviceRegistry();
        registry.Register(CreateDevice("plc.main"));

        var act = () => registry.Register(CreateDevice("PLC.MAIN")); // 大小写不敏感判重

        act.Should().Throw<ArgumentException>().WithMessage("*plc.main*");
    }

    [Fact]
    public void Devices_AfterMultipleRegister_ReturnsAll()
    {
        var registry = new DeviceRegistry();
        registry.Register(CreateDevice("plc.main"));
        registry.Register(CreateDevice("scanner.com3"));

        registry.Devices.Should().HaveCount(2)
            .And.Contain(d => d.Info.DeviceId == "plc.main")
            .And.Contain(d => d.Info.DeviceId == "scanner.com3");
    }

    [Fact]
    public void DeviceRegistered_FiresOnRegister()
    {
        var registry = new DeviceRegistry();
        IDevice? received = null;
        registry.DeviceRegistered += (_, device) => received = device;
        var device = CreateDevice("plc.main");

        registry.Register(device);

        received.Should().Be(device);
    }

    private static IDevice CreateDevice(string deviceId)
    {
        var device = Substitute.For<IDevice>();
        device.Info.Returns(new DeviceInfo(deviceId, deviceId, DeviceType.Plc, "Test"));
        return device;
    }
}
