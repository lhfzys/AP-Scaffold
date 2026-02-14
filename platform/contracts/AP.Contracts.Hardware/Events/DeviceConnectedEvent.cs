using MediatR;

namespace AP.Contracts.Hardware.Events;

/// <summary>
/// 设备已连接事件
/// </summary>
public record DeviceConnectedEvent(
    string DeviceName,
    DateTime Timestamp
) : INotification;