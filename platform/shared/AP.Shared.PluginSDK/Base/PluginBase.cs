using AP.Core.PluginFramework.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AP.Shared.PluginSDK.Base;

/// <summary>
/// 插件基类 (提供标准日志和生命周期空实现)
/// </summary>
public abstract class PluginBase : IConfigurablePlugin
{
    protected ILogger Logger { get; private set; } = null!;

    /// <summary>
    /// 服务提供者 (在 InitializeAsync 后可用)
    /// </summary>
    protected IServiceProvider ServiceProvider { get; private set; } = null!;

    protected PluginBase(ILogger logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// [可选覆盖] 配置服务注册
    /// </summary>
    public virtual void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // 默认不注册任何服务，子类可覆盖
        Logger.LogDebug("插件 {Name} 配置服务...", GetType().Name);
    }

    /// <summary>
    /// [可选覆盖] 初始化资源
    /// </summary>
    public virtual Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        ServiceProvider = serviceProvider;
        Logger.LogDebug("插件 {Name} 已初始化", GetType().Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// [可选覆盖] 启动业务逻辑
    /// </summary>
    public virtual Task StartAsync(CancellationToken ct = default)
    {
        Logger.LogInformation("插件 {Name} 已启动", GetType().Name);
        return Task.CompletedTask;
    }

    /// <summary>
    /// [可选覆盖] 停止并清理资源
    /// </summary>
    public virtual Task StopAsync(CancellationToken ct = default)
    {
        Logger.LogInformation("插件 {Name} 已停止", GetType().Name);
        return Task.CompletedTask;
    }
}