namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备抽象（Device Runtime Model 的契约层核心）。
/// 统一 PLC、扫码枪与未来各类设备的身份、状态与连接生命周期。
/// 刻意精简：读写能力不属于本接口——数据访问走 Tag 层（T4.x），避免万能接口。
/// 连接状态以 <see cref="State"/> 为唯一事实来源（不提供 IsConnected 之类的派生快照）。
/// </summary>
public interface IDevice
{
    /// <summary>设备静态身份信息。</summary>
    DeviceInfo Info { get; }

    /// <summary>当前连接状态（唯一事实来源）。</summary>
    DeviceConnectionState State { get; }

    /// <summary>连接状态迁移通知（沿迁移边触发）。</summary>
    event EventHandler<DeviceConnectionTransition>? Transitioned;

    /// <summary>建立连接（或启动连接监督），失败自愈策略由设备实现决定。</summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>断开连接。</summary>
    Task DisconnectAsync();
}
