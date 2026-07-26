using AP.Contracts.Hardware.DeviceRuntime;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 一次点采集完成事件参数（T4.5 的变化通知钩子）。
/// </summary>
public sealed class TagPolledEventArgs : EventArgs
{
    public TagPolledEventArgs(string name, TagValue value, bool changed)
    {
        Name = name;
        Value = value;
        Changed = changed;
    }

    /// <summary>点名。</summary>
    public string Name { get; }

    /// <summary>写入最新值表后的值（含 Version）。</summary>
    public TagValue Value { get; }

    /// <summary>值或质量戳是否发生变化。</summary>
    public bool Changed { get; }
}

/// <summary>
/// Tag 采集引擎：按采集配置的生效间隔分组轮询点表（只读点跳过只写点），
/// 结果写入最新值表。按点逐个读取（批量合并待带类型的批量契约落地后单独立项接入）。
/// 只写最新值表，不发布事件——变化通知是 T4.5 的职责（经 <see cref="TagPolled"/> 钩子）。
/// </summary>
public sealed class TagAcquisitionEngine : IDisposable
{
    private readonly ITagTable _tagTable;
    private readonly TagAcquisitionConfig _config;
    private readonly ITagService _tagService;
    private readonly LatestTagValueStore _store;
    private readonly Microsoft.Extensions.Logging.ILogger<TagAcquisitionEngine> _logger;

    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cts;
    private List<Task> _loops = [];

    public TagAcquisitionEngine(
        ITagTable tagTable,
        TagAcquisitionConfig config,
        ITagService tagService,
        LatestTagValueStore store,
        Microsoft.Extensions.Logging.ILogger<TagAcquisitionEngine> logger)
    {
        _tagTable = tagTable;
        _config = config;
        _tagService = tagService;
        _store = store;
        _logger = logger;
    }

    /// <summary>每次点采集完成后触发（变化通知钩子）。</summary>
    public event EventHandler<TagPolledEventArgs>? TagPolled;

    /// <summary>是否正在运行。</summary>
    public bool IsRunning { get { lock (_lifecycleGate) return _cts != null; } }

    /// <summary>启动采集（幂等）。</summary>
    public void Start()
    {
        lock (_lifecycleGate)
        {
            if (_cts != null) return;
            _cts = new CancellationTokenSource();

            // 只写点不参与轮询；按生效间隔分组，每组一个周期循环
            var groups = _tagTable.Tags
                .Where(t => t.Definition.Access != TagAccess.WriteOnly)
                .GroupBy(t => _config.GetIntervalMs(t.Definition.Name));

            foreach (var group in groups)
            {
                var intervalMs = group.Key;
                var tags = group.ToList();
                _loops.Add(Task.Run(() => RunGroupLoopAsync(intervalMs, tags, _cts.Token)));
            }

            _logger.LogInformation("Tag 采集引擎已启动: {TagCount} 个点 / {GroupCount} 个采集组",
                _loops.Count == 0 ? 0 : groups.Sum(g => g.Count()), _loops.Count);
        }
    }

    /// <summary>停止采集（取消并等待全部循环退出；幂等）。</summary>
    public void Stop()
    {
        List<Task> loops;
        CancellationTokenSource? cts;
        lock (_lifecycleGate)
        {
            if (_cts == null) return;
            cts = _cts;
            loops = _loops;
            _cts = null;
            _loops = [];
        }

        try { cts.Cancel(); }
        catch (ObjectDisposedException) { /* 已释放视为已停止 */ }

        try { Task.WaitAll(loops.ToArray(), TimeSpan.FromSeconds(5)); }
        catch { /* 停止语义优先，等待异常忽略 */ }

        cts.Dispose();
        _logger.LogInformation("Tag 采集引擎已停止");
    }

    private async Task RunGroupLoopAsync(int intervalMs, List<ResolvedTag> tags, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

        // 启动即先采一轮，之后按周期采集
        do
        {
            foreach (var tag in tags)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    var value = await _tagService.ReadAsync(tag.Definition.Name, ct);
                    var (stored, changed) = _store.Update(
                        tag.Definition.Name, value.Value, value.Quality, value.Error);
                    TagPolled?.Invoke(this, new TagPolledEventArgs(tag.Definition.Name, stored, changed));
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex)
                {
                    // 单点异常不影响整组采集（TagService 通信失败已返回 Bad，此处兜底意外）
                    _logger.LogWarning(ex, "Tag 采集异常: {Tag}", tag.Definition.Name);
                }
            }
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    public void Dispose() => Stop();
}
