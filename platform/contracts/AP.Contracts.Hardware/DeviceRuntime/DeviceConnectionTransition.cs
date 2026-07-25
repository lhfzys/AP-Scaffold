namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备连接状态迁移记录（契约层轻量数据形状）。
/// 由设备实现从内部状态机事件转换发布，供 UI、审计、告警等消费。
/// </summary>
public sealed record DeviceConnectionTransition(
    DeviceConnectionState From,
    DeviceConnectionState To,
    string? Reason,
    DateTime Timestamp);
