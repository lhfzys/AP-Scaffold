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
/// Tag 采集引擎：按采集配置的生效间隔分组轮询点表（只写点跳过），结果写入最新值表。
/// PLC 设备且在线时优先**带类型批量读**（每周期一次往返）；
/// 驱动不支持批量 → 永久降级逐点；整批失败 → 本轮降级逐点（部分坏点不拖死整组）；
/// 设备未连接 → 直接逐点（TagService 快速返回 Bad，不在注定失败的批量上空等重试）。
/// 只写最新值表，不发布事件——变化通知是 T4.5 的职责（经 <see cref="TagPolled"/> 钩子）。
/// </summary>
public sealed class TagAcquisitionEngine : ITagAcquisitionStatus, IDisposable
{
    private readonly ITagTable _tagTable;
    private readonly TagAcquisitionConfig _config;
    private readonly ITagService _tagService;
    private readonly IPlcTypedBatchRead _typedBatchRead;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly LatestTagValueStore _store;
    private readonly ILogger<TagAcquisitionEngine> _logger;

    private readonly object _lifecycleGate = new();
    private CancellationTokenSource? _cts;
    private List<Task> _loops = [];
    private volatile bool _batchDisabled;
    private long _totalReads;
    private long _failedReads;

    public TagAcquisitionEngine(
        ITagTable tagTable,
        TagAcquisitionConfig config,
        ITagService tagService,
        IPlcTypedBatchRead typedBatchRead,
        IDeviceRegistry deviceRegistry,
        LatestTagValueStore store,
        ILogger<TagAcquisitionEngine> logger)
    {
        _tagTable = tagTable;
        _config = config;
        _tagService = tagService;
        _typedBatchRead = typedBatchRead;
        _deviceRegistry = deviceRegistry;
        _store = store;
        _logger = logger;
    }

    /// <summary>每次点采集完成后触发（变化通知钩子）。</summary>
    public event EventHandler<TagPolledEventArgs>? TagPolled;

    /// <summary>是否正在运行。</summary>
    public bool IsRunning { get { lock (_lifecycleGate) return _cts != null; } }

    /// <summary>自启动以来累计读取点数（含失败）。</summary>
    public long TotalReads => Interlocked.Read(ref _totalReads);

    /// <summary>自启动以来累计失败点数（Bad 质量/读取异常）。</summary>
    public long FailedReads => Interlocked.Read(ref _failedReads);

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
                .GroupBy(t => _config.GetIntervalMs(t.Definition.Name))
                .ToList();

            foreach (var group in groups)
            {
                var intervalMs = group.Key;
                var tags = group.ToList();
                _loops.Add(Task.Run(() => RunGroupLoopAsync(intervalMs, tags, _cts.Token)));
            }

            _logger.LogInformation("Tag 采集引擎已启动: {TagCount} 个点 / {GroupCount} 个采集组",
                groups.Sum(g => g.Count()), _loops.Count);
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
            if (ct.IsCancellationRequested) return;
            await PollOnceAsync(tags, ct);
        }
        while (await timer.WaitForNextTickAsync(ct));
    }

    /// <summary>一轮采集：在线 PLC 点优先批量，其余逐点。</summary>
    private async Task PollOnceAsync(List<ResolvedTag> tags, CancellationToken ct)
    {
        var batchCandidates = new List<ResolvedTag>();
        var individual = new List<ResolvedTag>();

        foreach (var tag in tags)
        {
            var device = _deviceRegistry.Find(tag.Definition.DeviceId);
            if (!_batchDisabled && device is { Info.Type: DeviceType.Plc }
                && device.State == DeviceConnectionState.Connected)
                batchCandidates.Add(tag);
            else
                individual.Add(tag);
        }

        if (batchCandidates.Count > 0)
        {
            try
            {
                var items = batchCandidates
                    .Select(t => new BatchReadItem(t.NormalizedAddress, t.Definition.DataType))
                    .ToList();
                var values = await _typedBatchRead.ReadBatchAsync(items, ct);

                foreach (var tag in batchCandidates)
                {
                    if (values.TryGetValue(tag.NormalizedAddress, out var value))
                        Publish(tag.Definition.Name, value, TagQuality.Good, null);
                    else
                        Publish(tag.Definition.Name, null, TagQuality.Bad, "批量结果缺少该地址");
                }
            }
            catch (NotSupportedException)
            {
                _batchDisabled = true;
                _logger.LogInformation("当前 PLC 驱动不支持带类型批量读取，采集降级为逐点读取");
                individual.AddRange(batchCandidates);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Tag 批量读取失败，本轮降级为逐点读取");
                individual.AddRange(batchCandidates);
            }
        }

        foreach (var tag in individual)
        {
            if (ct.IsCancellationRequested) return;
            await PollSingleAsync(tag, ct);
        }
    }

    private async Task PollSingleAsync(ResolvedTag tag, CancellationToken ct)
    {
        try
        {
            var value = await _tagService.ReadAsync(tag.Definition.Name, ct);
            Publish(tag.Definition.Name, value.Value, value.Quality, value.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // 单点异常不影响整组采集（TagService 通信失败已返回 Bad，此处兜底意外）
            _logger.LogWarning(ex, "Tag 采集异常: {Tag}", tag.Definition.Name);
        }
    }

    /// <summary>写入最新值表并触发采集完成钩子（同时累计读次统计：Bad 计失败）。</summary>
    private void Publish(string name, object? value, TagQuality quality, string? error)
    {
        Interlocked.Increment(ref _totalReads);
        if (quality != TagQuality.Good)
            Interlocked.Increment(ref _failedReads);

        var (stored, changed) = _store.Update(name, value, quality, error);
        TagPolled?.Invoke(this, new TagPolledEventArgs(name, stored, changed));
    }

    public void Dispose() => Stop();
}
