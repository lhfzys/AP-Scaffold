namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备静态身份信息。
/// </summary>
/// <param name="DeviceId">稳定标识（如 "plc.main"、"scanner.com3"），注册表键，单机单设备起步、按多设备预留。</param>
/// <param name="Name">显示名。</param>
/// <param name="Type">粗粒度设备类型。</param>
/// <param name="DriverType">驱动类型标识（如 "Mitsubishi"/"Siemens"/"Omron"/"Serial"）。</param>
public sealed record DeviceInfo(
    string DeviceId,
    string Name,
    DeviceType Type,
    string DriverType)
{
    /// <summary>设备分组（预留：设备管理界面分组展示，当前无消费者）。</summary>
    public string? Group { get; init; }

    /// <summary>描述（预留：设备管理界面展示，当前无消费者）。</summary>
    public string? Description { get; init; }
}
