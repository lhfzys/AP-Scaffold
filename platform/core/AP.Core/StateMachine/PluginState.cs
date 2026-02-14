namespace AP.Core.StateMachine;

/// <summary>
/// 插件生命周期状态枚举
/// </summary>
public enum PluginState
{
    /// <summary>
    /// 未加载
    /// </summary>
    Unloaded = 0,

    /// <summary>
    /// 已发现 (文件存在，但未加载程序集)
    /// </summary>
    Discovered = 1,

    /// <summary>
    /// 正在加载 (正在加载程序集和依赖)
    /// </summary>
    Loading = 2,

    /// <summary>
    /// 已加载 (程序集已加载到内存)
    /// </summary>
    Loaded = 3,

    /// <summary>
    /// 正在初始化 (执行 InitializeAsync)
    /// </summary>
    Initializing = 4,

    /// <summary>
    /// 已初始化 (依赖注入完成)
    /// </summary>
    Initialized = 5,

    /// <summary>
    /// 正在启动 (执行 StartAsync)
    /// </summary>
    Starting = 6,

    /// <summary>
    /// 运行中 (健康)
    /// </summary>
    Running = 7,

    /// <summary>
    /// 运行中 (降级/部分功能不可用)
    /// </summary>
    Degraded = 8,

    /// <summary>
    /// 正在停止 (执行 StopAsync)
    /// </summary>
    Stopping = 9,

    /// <summary>
    /// 已停止
    /// </summary>
    Stopped = 10,

    /// <summary>
    /// 失败 (发生不可恢复错误)
    /// </summary>
    Failed = 11,

    /// <summary>
    /// 已冻结 (被禁用但未卸载)
    /// </summary>
    Frozen = 12,

    /// <summary>
    /// 已废弃 (标记为过时)
    /// </summary>
    Deprecated = 13
}