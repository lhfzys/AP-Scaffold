namespace AP.Core.Enums;

/// <summary>
/// 插件操作执行结果
/// </summary>
public enum PluginExecutionResult
{
    Success = 0,
    Failed = 1,
    Cancelled = 2,
    Timeout = 3,
    DependencyMissing = 4,
    AccessDenied = 5
}