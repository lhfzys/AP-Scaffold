using AP.Contracts.Hardware.DeviceRuntime;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 点表热重载编排（<see cref="ITagTableReloader"/> 实现）：
/// 换表（校验失败保留旧表继续运行）→ 采集引擎重启（分组与周期重建）→ 最新值表清理已删除点。
/// </summary>
public sealed class TagTableReloader : ITagTableReloader
{
    private readonly ITagTable _tagTable;
    private readonly TagAcquisitionEngine _engine;
    private readonly LatestTagValueStore _store;
    private readonly ILogger<TagTableReloader> _logger;

    public TagTableReloader(
        ITagTable tagTable,
        TagAcquisitionEngine engine,
        LatestTagValueStore store,
        ILogger<TagTableReloader> logger)
    {
        _tagTable = tagTable;
        _engine = engine;
        _store = store;
        _logger = logger;
    }

    /// <inheritdoc />
    public TagTableReloadResult Reload()
    {
        if (_tagTable is not TagTable concrete)
            return TagTableReloadResult.Failed(["当前点表实现不支持热重载"]);

        var errors = concrete.Reload();
        if (errors.Count > 0)
        {
            _logger.LogWarning("点表热重载失败，保留旧表继续运行: {Errors}", string.Join("; ", errors));
            return TagTableReloadResult.Failed(errors);
        }

        _engine.Restart();
        _store.PruneExcept(_tagTable.Tags.Select(t => t.Definition.Name).ToList());
        _logger.LogInformation("点表热重载完成: {TagCount} 个点已生效", _tagTable.Tags.Count);
        return TagTableReloadResult.Ok;
    }
}
