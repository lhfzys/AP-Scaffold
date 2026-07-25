namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 值（带质量戳、时间戳、版本号）。
/// </summary>
/// <param name="Value">值本体（按 TagDefinition.DataType 装箱；Quality=Bad 时为 null）。</param>
/// <param name="Quality">质量戳。</param>
/// <param name="Timestamp">采样时间（DateTimeOffset，不依赖本地时区假设）。</param>
/// <param name="Version">
/// 版本号：由最新值表（T4.4）写入时按点单调递增分配，0 = 从未更新；
/// 直接读取（不经过缓存）返回 0。缓存去重、订阅、UI 增量刷新的统一依据。
/// </param>
/// <param name="Error">失败原因（Quality=Bad 时填充，面向日志与诊断）。</param>
public sealed record TagValue(
    object? Value,
    TagQuality Quality,
    DateTimeOffset Timestamp,
    long Version = 0,
    string? Error = null)
{
    /// <summary>构造一个 Good 值。</summary>
    public static TagValue Good(object? value, long version = 0) =>
        new(value, TagQuality.Good, DateTimeOffset.Now, version);

    /// <summary>构造一个 Bad 值（通信失败是常态，用结果表达而非异常）。</summary>
    public static TagValue Bad(string error, long version = 0) =>
        new(null, TagQuality.Bad, DateTimeOffset.Now, version, error);
}
