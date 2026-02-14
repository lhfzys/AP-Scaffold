using Serilog.Core;
using Serilog.Events;

namespace AP.Infra.Logging.Enrichers;

/// <summary>
/// 线程ID增强器 - 在日志中附加 ThreadId 字段
/// </summary>
public class ThreadIdEnricher : ILogEventEnricher
{
    private const string PropertyName = "ThreadId";

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        // 实时获取当前线程 ID
        logEvent.AddPropertyIfAbsent(
            propertyFactory.CreateProperty(PropertyName, Environment.CurrentManagedThreadId));
    }
}