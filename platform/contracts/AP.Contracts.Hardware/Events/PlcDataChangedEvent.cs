using MediatR;

namespace AP.Contracts.Hardware.Events;

/// <summary>
/// PLC 数据变化事件 (用于 MediatR 发布)
/// </summary>
/// <param name="DeviceName">设备名称 (如 "Mitsubishi-Q")</param>
/// <param name="Address">地址/标签 (如 "D100")</param>
/// <param name="Value">新值</param>
/// <param name="Timestamp">时间戳</param>
public record PlcDataChangedEvent(
    string DeviceName,
    string Address,
    object Value,
    DateTime Timestamp
) : INotification;