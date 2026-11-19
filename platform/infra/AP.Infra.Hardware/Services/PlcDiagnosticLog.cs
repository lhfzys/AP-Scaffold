using AP.Contracts.Hardware.Services;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.Services;

/// <summary>有界 PLC 通讯日志（默认 500 条），线程安全；供“通讯日志”页实时查看。</summary>
public sealed class PlcDiagnosticLog : IPlcDiagnosticLog
{
    private const int DefaultCapacity = 500;
    private readonly int _capacity;
    private readonly Queue<PlcDiagnosticEntry> _entries = new();
    private readonly object _gate = new();
    private readonly ILogger<PlcDiagnosticLog> _logger;

    public PlcDiagnosticLog(ILogger<PlcDiagnosticLog> logger, int capacity = DefaultCapacity)
    {
        _logger = logger;
        _capacity = Math.Max(1, capacity);
    }

    public event EventHandler? Changed;

    public void Record(PlcDiagnosticEntry entry)
    {
        lock (_gate)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > _capacity)
                _entries.Dequeue();
        }

        if (entry.Success)
            _logger.LogDebug("[PLC] {Operation} {Address} -> {Value} ({ElapsedMs} ms)",
                entry.Operation, entry.Address, entry.Value, entry.ElapsedMilliseconds);
        else
            _logger.LogWarning("[PLC] {Operation} {Address} 失败: {Error} ({ElapsedMs} ms)",
                entry.Operation, entry.Address, entry.Error, entry.ElapsedMilliseconds);

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<PlcDiagnosticEntry> GetRecent(int max = 200)
    {
        lock (_gate)
            return _entries.Reverse().Take(Math.Max(1, max)).ToList();
    }

    public void Clear()
    {
        lock (_gate)
            _entries.Clear();

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
