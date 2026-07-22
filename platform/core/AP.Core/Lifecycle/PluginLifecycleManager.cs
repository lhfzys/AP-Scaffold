using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Loading;
using AP.Core.StateMachine;
using Microsoft.Extensions.Logging;

namespace AP.Core.Lifecycle;

/// <summary>
/// 插件生命周期管理器 (负责编排所有插件的初始化与启停)
/// 使用状态机跟踪每个插件的状态，支持有序启动和优雅停止
/// </summary>
public class PluginLifecycleManager
{
    private readonly ILogger<PluginLifecycleManager> _logger;
    private readonly List<PluginDescriptor> _loadedPlugins = new();

    // 存储每个插件的状态机
    private readonly Dictionary<string, PluginStateMachine> _stateMachines = new();

    public PluginLifecycleManager(ILogger<PluginLifecycleManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 注册已加载的插件（由 Bootstrapper 调用）
    /// 插件必须已经完成实例化
    /// 按照状态机规则依次转换: Unloaded → Discovered → Loading → Loaded
    /// </summary>
    public void RegisterPlugins(IEnumerable<PluginDescriptor> descriptors)
    {
        foreach (var descriptor in descriptors)
        {
            if (!descriptor.IsLoaded || descriptor.Instance == null)
                continue;

            var pluginId = descriptor.Metadata.Id;

            // 创建状态机（初始状态为 Unloaded）
            var stateMachine = new PluginStateMachine(pluginId, _logger);
            _stateMachines[pluginId] = stateMachine;

            // 按照状态机规则依次转换: Unloaded → Discovered → Loading → Loaded
            stateMachine.TransitionTo(PluginState.Discovered);
            stateMachine.TransitionTo(PluginState.Loading);
            stateMachine.TransitionTo(PluginState.Loaded);

            _loadedPlugins.Add(descriptor);
            _logger.LogDebug("插件已注册到生命周期管理器: {Name} ({Id})", descriptor.Metadata.Name, pluginId);
        }

        // 注册完成后按优先级升序排序，保证后续查询和启停顺序一致
        _loadedPlugins.Sort((a, b) => a.Metadata.Priority.CompareTo(b.Metadata.Priority));

        _logger.LogInformation("已注册 {Count} 个插件到生命周期管理器", _loadedPlugins.Count);
    }

    /// <summary>
    /// 初始化所有已注册的插件（按优先级顺序）
    /// </summary>
    public async Task InitializePluginsAsync(IServiceProvider rootProvider, CancellationToken ct = default)
    {
        _logger.LogInformation("=== 开始初始化插件 ===");

        foreach (var descriptor in _loadedPlugins.OrderBy(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];

            // 只有 Loaded 状态的插件才能初始化
            if (sm.CurrentState != PluginState.Loaded) continue;

            try
            {
                sm.TransitionTo(PluginState.Initializing);

                await descriptor.Instance!.InitializeAsync(rootProvider, ct);

                sm.TransitionTo(PluginState.Initialized);
                _logger.LogInformation("插件已初始化: {Name}", descriptor.Metadata.Name);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 初始化失败", descriptor.Metadata.Name);
                // 初始化失败不中断其他插件
            }
        }
    }

    /// <summary>
    /// 启动所有已初始化的插件（按优先级顺序）
    /// </summary>
    public async Task StartPluginsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("=== 开始启动插件 ===");

        foreach (var descriptor in _loadedPlugins.OrderBy(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];

            // 只有初始化成功的插件才能启动
            if (sm.CurrentState != PluginState.Initialized) continue;

            try
            {
                sm.TransitionTo(PluginState.Starting);

                await descriptor.Instance!.StartAsync(ct);

                sm.TransitionTo(PluginState.Running);
                _logger.LogInformation("插件已启动: {Name}", descriptor.Metadata.Name);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 启动失败", descriptor.Metadata.Name);
            }
        }
    }

    /// <summary>
    /// 停止所有运行中的插件（按优先级反序，实现优雅停止）
    /// </summary>
    public async Task StopPluginsAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("=== 开始停止插件 ===");

        // 停止时反向操作：优先级高的先停止（后启动的先停止）
        foreach (var descriptor in _loadedPlugins.OrderByDescending(p => p.Metadata.Priority))
        {
            var sm = _stateMachines[descriptor.Metadata.Id];

            // 只停止运行中或降级状态的插件
            if (sm.CurrentState != PluginState.Running && sm.CurrentState != PluginState.Degraded)
                continue;

            try
            {
                sm.TransitionTo(PluginState.Stopping);

                // 单插件停止独立超时：不响应 ct 的同步阻塞（Thread.Sleep/Task.Wait/驱动内同步调用）
                // 不再拖死整个关闭序列；超时或失败记录后继续停止其余插件
                await descriptor.Instance!.StopAsync(ct)
                    .WaitAsync(TimeSpan.FromSeconds(5), ct);

                sm.TransitionTo(PluginState.Stopped);
                _logger.LogInformation("插件已停止: {Name}", descriptor.Metadata.Name);
            }
            catch (Exception ex)
            {
                sm.TransitionTo(PluginState.Failed);
                _logger.LogError(ex, "插件 {Name} 停止失败", descriptor.Metadata.Name);
            }
        }
    }

    /// <summary>
    /// 获取指定插件的当前状态
    /// </summary>
    public PluginState? GetPluginState(string pluginId)
    {
        return _stateMachines.TryGetValue(pluginId, out var sm) ? sm.CurrentState : null;
    }

    /// <summary>
    /// 获取已加载的插件描述符
    /// </summary>
    public IReadOnlyList<PluginDescriptor> GetLoadedPlugins()
    {
        return _loadedPlugins.AsReadOnly();
    }

    /// <summary>
    /// 获取所有运行中的插件
    /// </summary>
    public IReadOnlyList<PluginDescriptor> GetRunningPlugins()
    {
        return _loadedPlugins
            .Where(p => _stateMachines.TryGetValue(p.Metadata.Id, out var sm) && sm.CurrentState == PluginState.Running)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// 获取所有失败的插件
    /// </summary>
    public IReadOnlyList<PluginDescriptor> GetFailedPlugins()
    {
        return _loadedPlugins
            .Where(p => _stateMachines.TryGetValue(p.Metadata.Id, out var sm) && sm.CurrentState == PluginState.Failed)
            .ToList()
            .AsReadOnly();
    }
}
