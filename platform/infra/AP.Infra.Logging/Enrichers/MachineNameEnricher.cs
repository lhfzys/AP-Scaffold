using Serilog.Core;
using Serilog.Events;

namespace AP.Infra.Logging.Enrichers;

/// <summary>
/// 机器名增强器 - 在日志中附加 MachineName 字段
/// </summary>
public class MachineNameEnricher : ILogEventEnricher
{
    private const string PropertyName = "MachineName";
    private readonly LogEventProperty _cachedProperty;

    public MachineNameEnricher()
    {
        // 缓存属性值，避免每次写日志都进行系统调用，提升性能
        _cachedProperty = new LogEventProperty(PropertyName, new ScalarValue(Environment.MachineName));
    }

    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        logEvent.AddPropertyIfAbsent(_cachedProperty);
    }
}