using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class PlcDeviceAdapterTests
{
    [Fact]
    public void Info_BuiltFromConfig_WithoutResolvingInnerDriver()
    {
        var active = CreateActive(Substitute.For<IPlcService>());
        var adapter = new PlcDeviceAdapter(active, Options.Create(new PlcOptions { DriverType = "Siemens" }));

        adapter.Info.DeviceId.Should().Be("plc.main");
        adapter.Info.Type.Should().Be(DeviceType.Plc);
        adapter.Info.DriverType.Should().Be("Siemens");
    }

    [Fact]
    public void State_InnerNotDevice_ReturnsDisconnected()
    {
        var active = CreateActive(Substitute.For<IPlcService>()); // 驱动未实现 IDevice
        var adapter = new PlcDeviceAdapter(active, Options.Create(new PlcOptions()));

        adapter.State.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void State_ForwardsInnerDeviceState()
    {
        var driver = Substitute.For<IPlcService, IDevice>();
        ((IDevice)driver).State.Returns(DeviceConnectionState.Connected);
        var adapter = new PlcDeviceAdapter(CreateActive(driver), Options.Create(new PlcOptions()));

        adapter.State.Should().Be(DeviceConnectionState.Connected);
    }

    [Fact]
    public void Transitioned_ForwardsInnerDeviceEvent()
    {
        var driver = new FakeDeviceDriver();
        var adapter = new PlcDeviceAdapter(CreateActive(driver), Options.Create(new PlcOptions()));
        DeviceConnectionTransition? received = null;
        adapter.Transitioned += (_, transition) => received = transition;

        var transition = new DeviceConnectionTransition(
            DeviceConnectionState.Connecting, DeviceConnectionState.Connected, "连接成功", DateTime.Now);
        driver.RaiseTransition(transition);

        received.Should().Be(transition);
    }

    [Fact]
    public async Task ConnectAsync_ForwardsToInnerDriver()
    {
        var driver = Substitute.For<IPlcService, IDevice>();
        var adapter = new PlcDeviceAdapter(CreateActive(driver), Options.Create(new PlcOptions()));

        await adapter.ConnectAsync();

        await driver.Received(1).ConnectAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DisconnectAsync_ForwardsToInnerDriver()
    {
        var driver = Substitute.For<IPlcService, IDevice>();
        var adapter = new PlcDeviceAdapter(CreateActive(driver), Options.Create(new PlcOptions()));

        await adapter.DisconnectAsync();

        await driver.Received(1).DisconnectAsync();
    }

    private static ActivePlcService CreateActive(IPlcService driver)
    {
        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Test");
        factory.SupportedFeatures.Returns(PlcServiceFeatures.BasicReadWrite);
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(driver);

        var registry = new PlcDriverRegistry();
        registry.Register(factory);

        return new ActivePlcService(
            Options.Create(new PlcOptions { DriverType = "Test" }),
            registry,
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<ActivePlcService>>());
    }

    /// <summary>
    /// 手写 Fake：NSubstitute 的事件 Raise 要求 EventArgs 约束，DeviceConnectionTransition 是 record，故用手写实现。
    /// </summary>
    private sealed class FakeDeviceDriver : IPlcService, IDevice
    {
        public DeviceInfo Info { get; } = new("plc.main", "Fake PLC", DeviceType.Plc, "Test");
        public DeviceConnectionState State { get; set; } = DeviceConnectionState.Disconnected;
        public event EventHandler<DeviceConnectionTransition>? Transitioned;
        public PlcServiceFeatures SupportedFeatures => PlcServiceFeatures.BasicReadWrite;

        public void RaiseTransition(DeviceConnectionTransition transition) => Transitioned?.Invoke(this, transition);

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
        public Task<bool> IsConnectedAsync() => Task.FromResult(State == DeviceConnectionState.Connected);
        public Task<T> ReadAsync<T>(string address, CancellationToken ct = default) => Task.FromResult(default(T)!);
        public Task WriteAsync<T>(string address, T value, CancellationToken ct = default) => Task.CompletedTask;
    }
}
