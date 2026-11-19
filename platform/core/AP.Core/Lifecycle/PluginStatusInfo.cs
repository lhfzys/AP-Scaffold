using AP.Core.StateMachine;

namespace AP.Core.Lifecycle;

/// <summary>插件加载/运行状态快照（用于启动日志与插件状态页）。</summary>
public sealed record PluginStatusInfo(
    string Id,
    string Name,
    int Priority,
    bool Required,
    PluginState State);
