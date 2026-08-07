namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 点表（运行时视图）：全系统 Tag 的统一查询入口。
/// 启动时加载并校验（快速失败）；支持热重载——重载成功后 Tags/Acquisition 返回新快照
/// （见 <see cref="ITagTableReloader"/>），消费者每次访问现取，无需缓存。
/// </summary>
public interface ITagTable
{
    /// <summary>全部已解析 Tag（快照）。</summary>
    IReadOnlyCollection<ResolvedTag> Tags { get; }

    /// <summary>采集配置（tags.json "Acquisition" 节；缺失=默认值，热重载后为新值）。</summary>
    TagAcquisitionConfig Acquisition { get; }

    /// <summary>按点名查找（大小写不敏感），不存在返回 null。</summary>
    ResolvedTag? Find(string name);
}
