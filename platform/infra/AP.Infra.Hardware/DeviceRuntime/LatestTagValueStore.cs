using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 最新值表：全部 Tag 最新采集值的唯一存放处（线程安全）。
/// 写入时按点单调递增 <see cref="TagValue.Version"/>；订阅者读最新值而不是打设备（缓存职责）。
/// </summary>
public sealed class LatestTagValueStore : ILatestTagValueStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, TagValue> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 写入一次采样：Version 递增；返回（新值, 是否变化）。
    /// 变化判定 = 值或质量戳任一变化（订阅方据此决定是否通知）。
    /// </summary>
    public (TagValue Value, bool Changed) Update(string name, object? value, TagQuality quality, string? error = null)
    {
        lock (_gate)
        {
            _values.TryGetValue(name, out var old);
            var next = new TagValue(value, quality, DateTimeOffset.Now, (old?.Version ?? 0) + 1, error);
            var changed = old == null || !Equals(old.Value, value) || old.Quality != quality;
            _values[name] = next;
            return (next, changed);
        }
    }

    /// <summary>读取指定点最新值（未采集过返回 null）。</summary>
    public TagValue? Get(string name)
    {
        lock (_gate) return _values.GetValueOrDefault(name);
    }

    /// <summary>全部最新值快照。</summary>
    public IReadOnlyDictionary<string, TagValue> Snapshot()
    {
        lock (_gate) return new Dictionary<string, TagValue>(_values, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>清理不在点名集内的残留值（点表热重载后调用，移除已删除点）。</summary>
    public void PruneExcept(IReadOnlyCollection<string> names)
    {
        var keep = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        lock (_gate)
        {
            var stale = _values.Keys.Where(k => !keep.Contains(k)).ToList();
            foreach (var key in stale)
                _values.Remove(key);
        }
    }
}
