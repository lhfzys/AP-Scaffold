namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 采集配置（tags.json 的 "Acquisition" 节，可选；缺失=默认值）。
/// 采集策略（周期）属于本配置而非 Tag 定义——Tag 回答"这个点是什么"，本配置回答"怎么采"。
/// </summary>
public sealed class TagAcquisitionConfig
{
    /// <summary>默认采集周期（毫秒），缺省 1000。</summary>
    public int DefaultIntervalMs { get; set; } = 1000;

    /// <summary>按点名的周期覆盖（毫秒），缺省空。</summary>
    public Dictionary<string, int> Overrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>取指定点名的生效采集周期。</summary>
    public int GetIntervalMs(string tagName)
    {
        return Overrides.TryGetValue(tagName, out var interval) && interval > 0
            ? interval
            : DefaultIntervalMs;
    }
}
