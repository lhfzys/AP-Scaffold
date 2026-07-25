using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class ConnectionSupervisorTests
{
    private static ConnectionSupervisorOptions FastOptions => new()
    {
        HeartbeatInterval = TimeSpan.FromMilliseconds(20),
        ReconnectBackoff = TimeSpan.FromMilliseconds(20),
        SupervisorRestartDelay = TimeSpan.FromMilliseconds(20),
        StopTimeout = TimeSpan.FromSeconds(2),
    };

    [Fact]
    public async Task Start_WhenDisconnected_AttemptsConnectAndTransitionsToConnected()
    {
        var sm = new DeviceConnectionStateMachine();
        var connectCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => { connectCalls++; return Task.FromResult(ConnectionAttemptResult.Ok()); },
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        supervisor.Start();

        await WaitUntilAsync(() => sm.CurrentState == DeviceConnectionState.Connected, "应转为 Connected");
        connectCalls.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConnectAttempted_FiresWithResultAndDuration()
    {
        var sm = new DeviceConnectionStateMachine();
        ConnectionAttemptedEventArgs? received = null;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);
        supervisor.ConnectAttempted += (_, args) => received = args;

        supervisor.Start();

        await WaitUntilAsync(() => received != null, "ConnectAttempted 应触发");
        received!.Result.Success.Should().BeTrue();
        received.Duration.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Fact]
    public async Task Connected_ProbeFailure_TransitionsToReconnecting_ThenRecovers()
    {
        var sm = new DeviceConnectionStateMachine();
        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Connected);

        var transitions = new List<(DeviceConnectionState From, DeviceConnectionState To)>();
        sm.Transitioned += (_, args) => transitions.Add((args.From, args.To));

        var probeCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            _ =>
            {
                probeCalls++;
                // 第一次心跳失败（掉线），之后 connect 成功恢复
                return Task.FromResult(probeCalls == 1
                    ? ConnectionAttemptResult.Fail("心跳超时")
                    : ConnectionAttemptResult.Ok());
            },
            FastOptions);

        supervisor.Start();

        await WaitUntilAsync(
            () => transitions.Contains((DeviceConnectionState.Connected, DeviceConnectionState.Reconnecting))
               && sm.CurrentState == DeviceConnectionState.Connected,
            "应先掉线再恢复");
        transitions.Should().Contain((DeviceConnectionState.Reconnecting, DeviceConnectionState.Connected));
    }

    [Fact]
    public async Task Connected_ProbeSuccess_KeepsConnected()
    {
        var sm = new DeviceConnectionStateMachine();
        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Connected);

        var probeCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            _ => { probeCalls++; return Task.FromResult(ConnectionAttemptResult.Ok()); },
            FastOptions);

        supervisor.Start();
        await WaitUntilAsync(() => probeCalls >= 3, "心跳应持续探测");

        sm.CurrentState.Should().Be(DeviceConnectionState.Connected);
    }

    [Fact]
    public async Task ConnectFailure_KeepsReconnecting_AndRetriesUntilSuccess()
    {
        var sm = new DeviceConnectionStateMachine();
        var connectCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ =>
            {
                connectCalls++;
                // 前两次失败，第三次成功
                return Task.FromResult(connectCalls < 3
                    ? ConnectionAttemptResult.Fail("连接被拒绝")
                    : ConnectionAttemptResult.Ok());
            },
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        supervisor.Start();

        await WaitUntilAsync(() => sm.CurrentState == DeviceConnectionState.Connected, "重试后应连接成功");
        connectCalls.Should().BeGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ConnectActionThrows_TreatedAsFailedAttempt_NotCrash()
    {
        var sm = new DeviceConnectionStateMachine();
        var connectCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ =>
            {
                connectCalls++;
                if (connectCalls < 2) throw new InvalidOperationException("驱动内部错误");
                return Task.FromResult(ConnectionAttemptResult.Ok());
            },
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        supervisor.Start();

        await WaitUntilAsync(() => sm.CurrentState == DeviceConnectionState.Connected, "异常应转为失败尝试后继续");
        connectCalls.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task ProbeThrows_TreatedAsFailure_TransitionsToReconnecting()
    {
        var sm = new DeviceConnectionStateMachine();
        sm.Transition(DeviceConnectionState.Connecting);
        sm.Transition(DeviceConnectionState.Connected);

        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            _ => throw new ObjectDisposedException("client"), // 心跳读到已释放客户端
            FastOptions);

        supervisor.Start();

        await WaitUntilAsync(() => sm.CurrentState == DeviceConnectionState.Reconnecting, "探测异常应判掉线");
    }

    [Fact]
    public async Task SubscriberThrows_LoopFaultedFires_AndSupervisorRestarts()
    {
        var sm = new DeviceConnectionStateMachine();
        var connectCalls = 0;
        var faulted = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => { connectCalls++; return Task.FromResult(ConnectionAttemptResult.Ok()); },
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        var thrown = false;
        supervisor.ConnectAttempted += (_, _) =>
        {
            if (!thrown) { thrown = true; throw new InvalidOperationException("订阅方异常"); }
        };
        supervisor.LoopFaulted += (_, _) => faulted++;

        supervisor.Start();

        await WaitUntilAsync(() => faulted >= 1, "订阅方异常应触发 LoopFaulted");
        await WaitUntilAsync(() => connectCalls >= 2, "循环应重启并继续尝试");
    }

    [Fact]
    public async Task Stop_CancelsLoop_NoFurtherAttempts()
    {
        var sm = new DeviceConnectionStateMachine();
        var connectCalls = 0;
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => { connectCalls++; return Task.FromResult(ConnectionAttemptResult.Fail("连不上")); },
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        supervisor.Start();
        await WaitUntilAsync(() => connectCalls >= 1, "应已开始尝试");

        supervisor.Stop();
        var callsAfterStop = connectCalls;
        await Task.Delay(200);

        connectCalls.Should().Be(callsAfterStop);
        supervisor.IsRunning.Should().BeFalse();
    }

    [Fact]
    public void Start_Twice_IsIdempotent()
    {
        var sm = new DeviceConnectionStateMachine();
        using var supervisor = new ConnectionSupervisor(
            sm,
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            _ => Task.FromResult(ConnectionAttemptResult.Ok()),
            FastOptions);

        supervisor.Start();
        var act = () => supervisor.Start();

        act.Should().NotThrow();
        supervisor.IsRunning.Should().BeTrue();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new Xunit.Sdk.XunitException($"等待超时: {because}");
            await Task.Delay(10);
        }
    }
}
