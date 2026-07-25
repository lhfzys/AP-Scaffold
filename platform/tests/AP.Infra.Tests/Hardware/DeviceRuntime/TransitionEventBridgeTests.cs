using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TransitionEventBridgeTests
{
    [Fact]
    public async Task MappedTransition_PublishesEventWithDeviceNameAndReason()
    {
        var sm = new DeviceConnectionStateMachine();
        var published = new List<object>();
        var bridge = new TransitionEventBridge()
            .Map(DeviceConnectionState.Disconnected, DeviceConnectionState.Connecting,
                (device, reason) => $"连接中:{device}:{reason}");

        using var _ = bridge.Attach(sm, n => { published.Add(n); return Task.CompletedTask; }, "TestDevice");

        sm.TryTransition(DeviceConnectionState.Connecting, "开始连接");
        await Task.Delay(50); // 发布为异步火忘，留出让渡时间

        published.Should().ContainSingle().Which.Should().Be("连接中:TestDevice:开始连接");
    }

    [Fact]
    public async Task UnmappedTransition_DoesNotPublish()
    {
        var sm = new DeviceConnectionStateMachine();
        var published = new List<object>();
        var bridge = new TransitionEventBridge()
            .Map(DeviceConnectionState.Disconnected, DeviceConnectionState.Disabled,
                (device, _) => "不应出现");

        using var _ = bridge.Attach(sm, n => { published.Add(n); return Task.CompletedTask; }, "TestDevice");

        sm.TryTransition(DeviceConnectionState.Connecting);
        await Task.Delay(50);

        published.Should().BeEmpty();
    }

    [Fact]
    public async Task MultipleMappings_EachFiresIndependently()
    {
        var sm = new DeviceConnectionStateMachine();
        var published = new List<object>();
        var bridge = new TransitionEventBridge()
            .Map(DeviceConnectionState.Disconnected, DeviceConnectionState.Connecting, (d, _) => "Connecting")
            .Map(DeviceConnectionState.Connecting, DeviceConnectionState.Connected, (d, _) => "Connected");

        using var _ = bridge.Attach(sm, n => { published.Add(n); return Task.CompletedTask; }, "Dev");

        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Connected);
        await Task.Delay(50);

        published.Should().Equal("Connecting", "Connected");
    }

    [Fact]
    public async Task Dispose_Unsubscribes_NoFurtherEvents()
    {
        var sm = new DeviceConnectionStateMachine();
        var published = new List<object>();
        var bridge = new TransitionEventBridge()
            .Map(DeviceConnectionState.Disconnected, DeviceConnectionState.Connecting, (d, _) => "X");

        var subscription = bridge.Attach(sm, n => { published.Add(n); return Task.CompletedTask; }, "Dev");
        subscription.Dispose();

        sm.TryTransition(DeviceConnectionState.Connecting);
        await Task.Delay(50);

        published.Should().BeEmpty();
    }
}
