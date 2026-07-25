namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 点表（只读运行时视图）：全系统 Tag 的统一查询入口。
/// 启动时加载并校验（快速失败），运行期只读。
/// </summary>
public interface ITagTable
{
    /// <summary>全部已解析 Tag（快照）。</summary>
    IReadOnlyCollection<ResolvedTag> Tags { get; }

    /// <summary>按点名查找（大小写不敏感），不存在返回 null。</summary>
    ResolvedTag? Find(string name);
}
