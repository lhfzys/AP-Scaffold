using MediatR;

namespace AP.Contracts.Hardware.Events;

public record DeviceConnectionFailedEvent(string DeviceName, string Reason) : INotification;