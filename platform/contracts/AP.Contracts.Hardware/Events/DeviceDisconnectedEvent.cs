using MediatR;

namespace AP.Contracts.Hardware.Events;

/// <summary>
/// 设备连接断开事件
/// </summary>
public record DeviceDisconnectedEvent(
    string DeviceName,
    string Reason,
    DateTime Timestamp
) : INotification;