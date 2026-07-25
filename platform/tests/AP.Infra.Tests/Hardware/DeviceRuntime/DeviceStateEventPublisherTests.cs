using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class DeviceStateEventPublisherTests
{
    [Fact]
    public async Task Attach_DeviceTransition_PublishesUnifiedEvent()
    {
        var mediator = Substitute.For<IMediator>();
        var publisher = new DeviceStateEventPublisher(mediator);
        var device = new FakeDevice("plc.main", "主 PLC", DeviceType.Plc);
        using var _ = publisher.Attach(device);

        var transition = new DeviceConnectionTransition(
            DeviceConnectionState.Connecting, DeviceConnectionState.Connected, "连接成功", DateTime.Now);
        device.RaiseTransition(transition);
        await Task.Delay(50); // 发布为异步火忘，留出让渡时间

        await mediator.Received(1).Publish(
            Arg.Is<DeviceStateChangedEvent>(e =>
                e.Info.DeviceId == "plc.main" && e.Transition == transition),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispose_Unsubscribes_NoFurtherEvents()
    {
        var mediator = Substitute.For<IMediator>();
        var publisher = new DeviceStateEventPublisher(mediator);
        var device = new FakeDevice("scanner.com3", "扫码枪", DeviceType.Scanner);

        var subscription = publisher.Attach(device);
        subscription.Dispose();

        device.RaiseTransition(new DeviceConnectionTransition(
            DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting, "掉线", DateTime.Now));
        await Task.Delay(50);

        await mediator.DidNotReceive().Publish(
            Arg.Any<DeviceStateChangedEvent>(), Arg.Any<CancellationToken>());
    }

    /// <summary>手写 Fake：IDevice 只需事件与身份信息。</summary>
    private sealed class FakeDevice : IDevice
    {
        public FakeDevice(string deviceId, string name, DeviceType type)
        {
            Info = new DeviceInfo(deviceId, name, type, "Test");
        }

        public DeviceInfo Info { get; }
        public DeviceConnectionState State => DeviceConnectionState.Disconnected;
        public event EventHandler<DeviceConnectionTransition>? Transitioned;

        public void RaiseTransition(DeviceConnectionTransition transition) => Transitioned?.Invoke(this, transition);

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync() => Task.CompletedTask;
    }
}
