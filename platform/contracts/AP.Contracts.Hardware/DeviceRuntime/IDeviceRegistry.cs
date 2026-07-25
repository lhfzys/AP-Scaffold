namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备注册表：平台内全部设备的统一登记与查找入口。
/// 单机单设备起步，API 按多设备设计（DeviceId 为键，大小写不敏感）。
/// </summary>
public interface IDeviceRegistry
{
    /// <summary>已注册的全部设备（快照）。</summary>
    IReadOnlyCollection<IDevice> Devices { get; }

    /// <summary>按 ID 查找设备，未注册返回 null。</summary>
    IDevice? Find(string deviceId);

    /// <summary>按 ID 获取设备，未注册抛 <see cref="KeyNotFoundException"/>。</summary>
    IDevice Get(string deviceId);

    /// <summary>注册设备，重复 ID 抛 <see cref="ArgumentException"/>。</summary>
    void Register(IDevice device);

    /// <summary>设备注册后触发（设备管理界面等的挂点）。</summary>
    event EventHandler<IDevice>? DeviceRegistered;
}
