using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Models;
using Microsoft.Extensions.Options;

namespace AP.Infra.Hardware.Services;

/// <summary>
/// PLC 的设备视图适配器（薄封装）：把 <see cref="ActivePlcService"/> 背后真实驱动的
/// <see cref="IDevice"/> 能力暴露为平台统一设备。
/// Info 从配置构建（无需等待懒加载）；状态/事件/连接转发到真实驱动（惰性解析）。
/// </summary>
public sealed class PlcDeviceAdapter : IDevice
{
    private readonly ActivePlcService _active;
    private EventHandler<DeviceConnectionTransition>? _transitioned;
    private bool _innerSubscribed;

    public PlcDeviceAdapter(ActivePlcService active, IOptions<PlcOptions> options)
    {
        _active = active;
        var driverType = options.Value.DriverType;
        Info = new DeviceInfo("plc.main", $"PLC ({driverType})", DeviceType.Plc, driverType);
    }

    /// <inheritdoc />
    public DeviceInfo Info { get; }

    /// <inheritdoc />
    public DeviceConnectionState State =>
        _active.InnerDevice?.State ?? DeviceConnectionState.Disconnected;

    /// <inheritdoc />
    public event EventHandler<DeviceConnectionTransition>? Transitioned
    {
        add
        {
            EnsureInnerSubscribed();
            _transitioned += value;
        }
        remove => _transitioned -= value;
    }

    /// <inheritdoc />
    public Task ConnectAsync(CancellationToken ct = default) => _active.ConnectAsync(ct);

    /// <inheritdoc />
    public Task DisconnectAsync() => _active.DisconnectAsync();

    /// <summary>
    /// 惰性订阅内层驱动事件（首个订阅者到来时才解析真实驱动并挂接）。
    /// </summary>
    private void EnsureInnerSubscribed()
    {
        if (_innerSubscribed) return;
        _innerSubscribed = true;

        if (_active.InnerDevice is { } inner)
            inner.Transitioned += (_, transition) => _transitioned?.Invoke(this, transition);
    }
}
