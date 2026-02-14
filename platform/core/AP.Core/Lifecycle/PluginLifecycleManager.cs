using AP.Core.Enums;
using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Loading;
using AP.Core.StateMachine;
using Microsoft.Extensions.Logging;

namespace AP.Core.Lifecycle;

/// <summary>
/// 插件生命周期管理器 (负责编排所有插件的初始化与启停)
/// </summary>
public class PluginLifecycleManager
{
    private readonly PluginLoader _loader;
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly List<PluginDescriptor> _loadedPlugins = new();

    // 存储每个插件的状态机
    private readonly Dictionary<string, PluginStateMachine> _stateMachines = new();

    public PluginLifecycleManager(PluginLoader loader, ILogger<PluginLifecycleManager> logger)
    {
        _loader = loader;
        _logger = logger;
    }

    /// <summary>
    /// 加载并初始化所有插件
    /// </summary>
    public async Task LoadPluginsAsync(AppRole role, IServiceProvider rootProvider, CancellationToken ct = default)
    {
        _logger.LogInformation("开始加载插件，运行角色: {Role}", role);

        // 1. 发现插件 (此时 DLL 已加载，但 Instance 未创建)
        var descriptors = _loader.DiscoverPlugins(role);

        foreach (var descriptor in descriptors)
            try
            {
                var pluginId = descriptor.Metadata.Id;

                // 创建状态机
                var stateMachine = new PluginStateMachine(pluginId, CreateLoggerForStateMachine(pluginId));
                _stateMachines[pluginId] = stateMachine;

                stateMachine.TransitionTo(PluginState.Discovered);

                // 2. 实例化插件
                // 注意：这里需要从 descriptor.LoadContext 加载，已经在 Loader 中处理好了 Assembly
                var instance = Activator.CreateInstance(descriptor.PluginType) as IPlugin;

                if (instance == null)
                    throw new InvalidOperationException($"无法实例化插件类型: {descriptor.PluginType.FullName}");

                descriptor.Instance = instance;
                descriptor.IsLoaded = true;
                _loadedPlugins.Add(descriptor);

                stateMachine.TransitionTo(PluginState.Loaded);
                _logger.LogInformation("插件已加载: {Name} ({Id})", descriptor.Metadata.Name, pluginId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件 {Id} 失败", descriptor.Metadata.Id);
                // 如果是必需插件失败，则抛出异常中断启动
                if (descriptor.Metadata.Required) throw;
            }

        // 3. 初始化插件 (按优先级顺序)
        foreach (var descriptor in _loadedPlugins.OrderBy(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];
            try
            {
                sm.TransitionTo(PluginState.Initializing);

                if (descriptor.Instance != null) await descriptor.Instance.InitializeAsync(rootProvider, ct);

                sm.TransitionTo(PluginState.Initialized);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 初始化失败", descriptor.Metadata.Name);
                if (descriptor.Metadata.Required) throw;
            }
        }
    }

    /// <summary>
    /// 启动所有插件
    /// </summary>
    public async Task StartPluginsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("正在启动所有插件...");

        foreach (var descriptor in _loadedPlugins.OrderBy(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];

            // 只有初始化成功的插件才能启动
            if (sm.CurrentState != PluginState.Initialized) continue;

            try
            {
                sm.TransitionTo(PluginState.Starting);

                if (descriptor.Instance != null) await descriptor.Instance.StartAsync(ct);

                sm.TransitionTo(PluginState.Running);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 启动失败", descriptor.Metadata.Name);
                // 启动失败通常不应导致整个应用崩溃，除非业务有特殊要求
            }
        }
    }

    /// <summary>
    /// 停止所有插件 (按优先级反序)
    /// </summary>
    public async Task StopPluginsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("正在停止所有插件...");

        // 停止时反向操作：优先级低的（后启动的）先停止
        foreach (var descriptor in _loadedPlugins.OrderByDescending(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];

            if (sm.CurrentState != PluginState.Running && sm.CurrentState != PluginState.Degraded) continue;

            try
            {
                sm.TransitionTo(PluginState.Stopping);

                if (descriptor.Instance != null) await descriptor.Instance.StopAsync(ct);

                sm.TransitionTo(PluginState.Stopped);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 停止失败", descriptor.Metadata.Name);
            }
        }
    }

    // 辅助方法：为状态机创建 Logger
    private ILogger CreateLoggerForStateMachine(string pluginId)
    {
        // 这里只是简单的演示，实际可以通过 LoggerFactory 创建
        return _logger;
    }

    /// <summary>
    /// 获取已加载的插件描述符
    /// </summary>
    public IReadOnlyList<PluginDescriptor> GetLoadedPlugins()
    {
        return _loadedPlugins.AsReadOnly();
    }
}