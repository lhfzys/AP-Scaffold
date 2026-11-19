using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Services;

namespace AP.Infra.Hardware.Services;

/// <summary>带类型批量读取的诊断装饰器：采集引擎走此接口，统一记录通讯日志。</summary>
public sealed class DiagnosticTypedBatchReader : IPlcTypedBatchRead
{
    private readonly IPlcTypedBatchRead _inner;
    private readonly IPlcDiagnosticLog _log;

    public DiagnosticTypedBatchReader(IPlcTypedBatchRead inner, IPlcDiagnosticLog log)
    {
        _inner = inner;
        _log = log;
    }

    public async Task<Dictionary<string, object>> ReadBatchAsync(
        IReadOnlyList<BatchReadItem> items,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        string? error = null;
        try
        {
            return await _inner.ReadBatchAsync(items, ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            _log.Record(new PlcDiagnosticEntry(
                DateTime.Now,
                "TypedBatchRead",
                $"{items.Count} 个点",
                string.Join(",", items.Select(i => i.Address)),
                sw.ElapsedMilliseconds,
                error is null,
                error));
        }
    }
}
