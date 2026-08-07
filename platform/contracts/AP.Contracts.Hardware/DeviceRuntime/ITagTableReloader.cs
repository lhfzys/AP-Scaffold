namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 点表热重载：重新加载 tags.json 并即时生效（采集分组与周期重建 + 已删除点最新值清理），无需重启应用。
/// 校验失败时保留旧点表继续运行（不因非法内容中断采集）。
/// </summary>
public interface ITagTableReloader
{
    /// <summary>执行一次热重载；失败时返回错误明细且运行中的点表不变。</summary>
    TagTableReloadResult Reload();
}

/// <summary>点表热重载结果。</summary>
public sealed record TagTableReloadResult(bool Success, IReadOnlyList<string> Errors)
{
    /// <summary>成功。</summary>
    public static TagTableReloadResult Ok { get; } = new(true, []);

    /// <summary>失败（文件读取或校验错误明细）。</summary>
    public static TagTableReloadResult Failed(IReadOnlyList<string> errors) => new(false, errors);
}
