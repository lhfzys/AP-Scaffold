namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 值质量戳（OPC 惯例三态）。
/// 通信失败是常态：读取失败返回 <see cref="Bad"/> 并携带原因，不抛异常（ERROR_HANDLING.md 的落地）。
/// </summary>
public enum TagQuality
{
    /// <summary>值可信。</summary>
    Good = 0,

    /// <summary>值可读但可信度存疑（如设备刚重连、值可能过期）。</summary>
    Uncertain = 1,

    /// <summary>值不可用（通信失败、地址非法、驱动不支持等，原因见 TagValue.Error）。</summary>
    Bad = 2,
}
