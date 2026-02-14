using MediatR;

namespace AP.Contracts.Hardware.Events;

public record DeviceConnectingEvent(string DeviceName) : INotification;