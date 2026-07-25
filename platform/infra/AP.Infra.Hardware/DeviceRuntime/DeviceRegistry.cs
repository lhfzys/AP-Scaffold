using System.Collections.Concurrent;
using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 设备注册表默认实现（线程安全，DeviceId 大小写不敏感）。
/// </summary>
public sealed class DeviceRegistry : IDeviceRegistry
{
    private readonly ConcurrentDictionary<string, IDevice> _devices = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public event EventHandler<IDevice>? DeviceRegistered;

    /// <inheritdoc />
    public IReadOnlyCollection<IDevice> Devices => _devices.Values.ToList();

    /// <inheritdoc />
    public IDevice? Find(string deviceId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        return _devices.TryGetValue(deviceId, out var device) ? device : null;
    }

    /// <inheritdoc />
    public IDevice Get(string deviceId)
    {
        return Find(deviceId)
            ?? throw new KeyNotFoundException($"未注册的设备: '{deviceId}'");
    }

    /// <inheritdoc />
    public void Register(IDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);
        ArgumentException.ThrowIfNullOrWhiteSpace(device.Info.DeviceId);

        if (!_devices.TryAdd(device.Info.DeviceId, device))
            throw new ArgumentException($"设备 ID 重复注册: '{device.Info.DeviceId}'", nameof(device));

        DeviceRegistered?.Invoke(this, device);
    }
}
