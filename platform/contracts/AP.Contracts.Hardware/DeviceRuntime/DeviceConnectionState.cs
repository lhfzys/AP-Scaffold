namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备连接状态（Device Runtime Model 的第一组件，协议无关）。
/// 适用于 PLC、扫码枪、相机、机器人、MQTT 等一切需要连接管理的设备；
/// 本枚举不得出现任何协议/品牌相关成员。
/// </summary>
public enum DeviceConnectionState
{
    /// <summary>未连接（初始状态 / 主动断开后的稳定态）。</summary>
    Disconnected = 0,

    /// <summary>正在建立连接（首次连接尝试进行中）。</summary>
    Connecting = 1,

    /// <summary>已连接，可正常通信。</summary>
    Connected = 2,

    /// <summary>连接丢失后正在自动重连（心跳失败 / 通信异常后的重试期间）。</summary>
    Reconnecting = 3,

    /// <summary>
    /// 故障态：不可自愈的错误（如配置错误、驱动初始化失败），需人工介入。
    /// 预留状态：供 DeviceRuntime / HealthMonitor 使用，当前驱动暂不进入此态。
    /// </summary>
    Faulted = 4,

    /// <summary>
    /// 已停用：设备被显式停用（维护模式），连接监督不再尝试连接。
    /// 预留状态：供 DeviceRuntime 维护模式使用，当前驱动暂不进入此态。
    /// </summary>
    Disabled = 5,
}
