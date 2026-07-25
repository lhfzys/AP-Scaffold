using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class DeviceConnectionStateMachineTests
{
    [Fact]
    public void InitialState_IsDisconnected()
    {
        var sm = new DeviceConnectionStateMachine();

        sm.CurrentState.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void TryTransition_ValidTransition_UpdatesStateAndReturnsTrue()
    {
        var sm = new DeviceConnectionStateMachine();

        var result = sm.TryTransition(DeviceConnectionState.Connecting, "开始连接");

        result.Should().BeTrue();
        sm.CurrentState.Should().Be(DeviceConnectionState.Connecting);
    }

    [Fact]
    public void TryTransition_InvalidTransition_ReturnsFalseAndKeepsState()
    {
        var sm = new DeviceConnectionStateMachine();

        // Disconnected 不能直接跳到 Connected
        var result = sm.TryTransition(DeviceConnectionState.Connected);

        result.Should().BeFalse();
        sm.CurrentState.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void TryTransition_SameState_IsInvalid()
    {
        var sm = new DeviceConnectionStateMachine();

        sm.TryTransition(DeviceConnectionState.Disconnected).Should().BeFalse();
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsInvalidOperationException()
    {
        var sm = new DeviceConnectionStateMachine();

        var act = () => sm.Transition(DeviceConnectionState.Connected);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Disconnected*Connected*");
    }

    [Fact]
    public void Transitioned_ValidTransition_FiresWithFromToAndReason()
    {
        var sm = new DeviceConnectionStateMachine();
        DeviceConnectionTransitionEventArgs? received = null;
        sm.Transitioned += (_, args) => received = args;

        sm.TryTransition(DeviceConnectionState.Connecting, "开始连接");

        received.Should().NotBeNull();
        received!.From.Should().Be(DeviceConnectionState.Disconnected);
        received.To.Should().Be(DeviceConnectionState.Connecting);
        received.Reason.Should().Be("开始连接");
        received.Timestamp.Should().BeCloseTo(DateTime.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Transitioned_InvalidTransition_DoesNotFire()
    {
        var sm = new DeviceConnectionStateMachine();
        var fired = 0;
        sm.Transitioned += (_, _) => fired++;

        sm.TryTransition(DeviceConnectionState.Connected);

        fired.Should().Be(0);
    }

    [Fact]
    public void WatchdogEquivalentSequence_AllTransitionsValid()
    {
        // 与现有 PLC 看门狗语义一一对应的典型生命周期
        var sm = new DeviceConnectionStateMachine();

        sm.TryTransition(DeviceConnectionState.Connecting).Should().BeTrue();    // 发起连接
        sm.TryTransition(DeviceConnectionState.Connected).Should().BeTrue();     // 连接成功
        sm.TryTransition(DeviceConnectionState.Reconnecting).Should().BeTrue();  // 心跳丢失
        sm.TryTransition(DeviceConnectionState.Connected).Should().BeTrue();     // 重连成功
        sm.TryTransition(DeviceConnectionState.Disconnected).Should().BeTrue();  // 主动断开
    }

    [Fact]
    public void Reconnecting_CanGiveUp_ToDisconnected()
    {
        var sm = new DeviceConnectionStateMachine();
        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Reconnecting); // 首次连接失败转重连

        sm.TryTransition(DeviceConnectionState.Disconnected, "放弃重连（主动停用）").Should().BeTrue();
    }

    [Fact]
    public void ReservedStates_FaultedAndDisabled_ReachablePerTransitionTable()
    {
        var sm = new DeviceConnectionStateMachine();

        // Connecting 失败且不可自愈 → Faulted；人工重试 → Connecting；维护停用 → Disabled；重新启用 → Disconnected
        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Faulted, "配置错误");
        sm.Transition(DeviceConnectionState.Connecting, "人工重试");
        sm.Transition(DeviceConnectionState.Disabled, "维护模式");
        sm.Transition(DeviceConnectionState.Disconnected, "重新启用");

        sm.CurrentState.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void Transitioned_SubscriberCanReenter_NoDeadlock()
    {
        // 事件在锁外发布：订阅方回调中读取状态甚至再次迁移都不会死锁
        var sm = new DeviceConnectionStateMachine();
        var reentered = false;
        sm.Transitioned += (_, args) =>
        {
            _ = sm.CurrentState; // 锁内读取应安全
            if (!reentered && args.To == DeviceConnectionState.Connecting)
            {
                reentered = true;
                sm.TryTransition(DeviceConnectionState.Disabled, "回调中停用").Should().BeTrue();
            }
        };

        sm.TryTransition(DeviceConnectionState.Connecting);

        reentered.Should().BeTrue();
        sm.CurrentState.Should().Be(DeviceConnectionState.Disabled);
    }

    [Fact]
    public void ProtocolAgnostic_CameraLifecycleScenario_WorksWithoutAnyPlcConcept()
    {
        // 协议无关性验证：用"网络相机"场景走完整个生命周期，
        // 状态机不知道也不关心设备是 PLC、相机还是 MQTT 客户端
        var cameraState = new DeviceConnectionStateMachine();
        var log = new List<string>();
        cameraState.Transitioned += (_, args) => log.Add($"{args.From}→{args.To}({args.Reason})");

        cameraState.Transition(DeviceConnectionState.Connecting, "RTSP 握手");
        cameraState.Transition(DeviceConnectionState.Connected, "流已建立");
        cameraState.Transition(DeviceConnectionState.Reconnecting, "帧超时");
        cameraState.Transition(DeviceConnectionState.Connected, "流恢复");

        log.Should().HaveCount(4);
        cameraState.CurrentState.Should().Be(DeviceConnectionState.Connected);
    }

    [Fact]
    public void ConcurrentTransitions_AreThreadSafe()
    {
        var sm = new DeviceConnectionStateMachine();
        sm.Transition(DeviceConnectionState.Connecting);

        // 多线程竞争迁移：所有调用要么成功要么安全失败，不允许抛异常或状态错乱
        var exceptions = new List<Exception>();
        Parallel.For(0, 100, i =>
        {
            try
            {
                sm.TryTransition(i % 2 == 0 ? DeviceConnectionState.Connected : DeviceConnectionState.Reconnecting);
                _ = sm.CurrentState;
            }
            catch (Exception ex)
            {
                lock (exceptions) exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
        sm.CurrentState.Should().BeOneOf(DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting);
    }
}
