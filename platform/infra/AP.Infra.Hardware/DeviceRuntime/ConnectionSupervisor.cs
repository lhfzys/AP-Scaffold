using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 连接监督器（Device Runtime Model 组件，协议无关）。
/// 在 <see cref="DeviceConnectionStateMachine"/> 之上实现"心跳探测 + 断线重连 + 监督者自愈"：
/// Connected 时周期性探测，探测失败转入 Reconnecting；Disconnected/Reconnecting 时逐轮尝试连接。
/// 是纯事件源，不依赖日志：日志由 ConnectionSupervisorLogger 作为消费者附加；
/// 设备语义（怎么连、怎么探测）由调用方以委托注入，PLC、串口、相机、MQTT 均可复用。
/// </summary>
public sealed class ConnectionSupervisor : IDisposable
{
    private readonly DeviceConnectionStateMachine _stateMachine;
    private readonly Func<CancellationToken, Task<ConnectionAttemptResult>> _connectAction;
    private readonly Func<CancellationToken, Task<ConnectionAttemptResult>> _probeAction;
    private readonly ConnectionSupervisorOptions _options;

    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private bool _running;

    public ConnectionSupervisor(
        DeviceConnectionStateMachine stateMachine,
        Func<CancellationToken, Task<ConnectionAttemptResult>> connectAction,
        Func<CancellationToken, Task<ConnectionAttemptResult>> probeAction,
        ConnectionSupervisorOptions? options = null)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _connectAction = connectAction ?? throw new ArgumentNullException(nameof(connectAction));
        _probeAction = probeAction ?? throw new ArgumentNullException(nameof(probeAction));
        _options = options ?? new ConnectionSupervisorOptions();
    }

    /// <summary>每次连接尝试完成后触发（含结果与耗时）。</summary>
    public event EventHandler<ConnectionAttemptedEventArgs>? ConnectAttempted;

    /// <summary>监督循环异常退出时触发（循环将自动重启）。</summary>
    public event EventHandler<SupervisorLoopFaultedEventArgs>? LoopFaulted;

    /// <summary>是否正在运行。</summary>
    public bool IsRunning
    {
        get { lock (_lifecycleGate) return _running; }
    }

    /// <summary>启动监督循环（幂等：重复调用不重复启动）。</summary>
    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_running) return;
            _running = true;
            _cts = new CancellationTokenSource();
            _loopTask = Task.Run(() => RunSupervisorLoopAsync(_cts.Token));
        }
    }

    /// <summary>停止监督循环（取消并带超时等待退出；幂等）。</summary>
    public void Stop()
    {
        Task? task;
        lock (_lifecycleGate)
        {
            if (!_running) return;
            _running = false;
            try { _cts?.Cancel(); }
            catch (ObjectDisposedException) { /* 已释放视为已停止 */ }
            task = _loopTask;
        }

        try { task?.Wait(_options.StopTimeout); }
        catch { /* 等待退出期间的异常不影响停止语义 */ }

        lock (_lifecycleGate)
        {
            _cts?.Dispose();
            _cts = null;
            _loopTask = null;
        }
    }

    /// <summary>
    /// 监督者循环：扫描循环异常退出时延迟重启，仅取消时真正退出。
    /// </summary>
    private async Task RunSupervisorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RunScanLoopAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LoopFaulted?.Invoke(this, new SupervisorLoopFaultedEventArgs(ex, _options.SupervisorRestartDelay));
                try
                {
                    await Task.Delay(_options.SupervisorRestartDelay, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// 扫描循环：按当前状态执行心跳探测或连接尝试。
    /// Connecting/Disabled/Faulted 状态下不动作（由外部流程管理）。
    /// </summary>
    private async Task RunScanLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(_options.HeartbeatInterval);

        // 启动即先扫一次（首连不等待首个心跳周期），之后按周期扫描
        do
        {
            switch (_stateMachine.CurrentState)
            {
                case DeviceConnectionState.Connected:
                    var probe = await ExecuteSafeAsync(_probeAction, ct);
                    if (!probe.Success)
                        _stateMachine.TryTransition(
                            DeviceConnectionState.Reconnecting,
                            probe.ErrorReason ?? "心跳探测失败");
                    break;

                case DeviceConnectionState.Disconnected:
                case DeviceConnectionState.Connecting:
                case DeviceConnectionState.Reconnecting:
                    // 监督器全权驱动连接状态：Disconnected 先转 Connecting，再按结果决定 Connected/Reconnecting。
                    // Connecting 出现在扫描入口 = 上一轮尝试被中断（如事件订阅方异常导致循环重启），
                    // 连接动作幂等（每次新建客户端），直接重新尝试即可恢复。
                    if (_stateMachine.CurrentState == DeviceConnectionState.Disconnected)
                        _stateMachine.TryTransition(DeviceConnectionState.Connecting, "开始连接");

                    var attempt = await ExecuteTimedAsync(_connectAction, ct);
                    ConnectAttempted?.Invoke(this, attempt);
                    if (attempt.Result.Success)
                    {
                        _stateMachine.TryTransition(DeviceConnectionState.Connected, "连接成功");
                    }
                    else
                    {
                        // 首连失败由 Connecting 转 Reconnecting；重连期间保持 Reconnecting
                        if (_stateMachine.CurrentState == DeviceConnectionState.Connecting)
                            _stateMachine.TryTransition(
                                DeviceConnectionState.Reconnecting,
                                attempt.Result.ErrorReason ?? "连接失败");
                        await Task.Delay(_options.ReconnectBackoff, ct);
                    }
                    break;

                // Disabled（停用）、Faulted（故障）：本循环不介入，等待外部流程处理
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    private async Task<ConnectionAttemptedEventArgs> ExecuteTimedAsync(
        Func<CancellationToken, Task<ConnectionAttemptResult>> action, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var result = await ExecuteSafeAsync(action, ct);
        sw.Stop();
        return new ConnectionAttemptedEventArgs(result, sw.Elapsed);
    }

    /// <summary>
    /// 执行动作并把异常统一转换为失败结果（设备语义层异常不击穿监督循环）。
    /// </summary>
    private static async Task<ConnectionAttemptResult> ExecuteSafeAsync(
        Func<CancellationToken, Task<ConnectionAttemptResult>> action, CancellationToken ct)
    {
        try
        {
            return await action(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // 取消语义原样上传，由监督者循环处理
        }
        catch (Exception ex)
        {
            return ConnectionAttemptResult.Fail(ex.Message, ex);
        }
    }

    public void Dispose() => Stop();
}
