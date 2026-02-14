namespace AP.Core.PluginFramework.Abstractions;

/// <summary>
/// 插件基础接口
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// 初始化插件(数据库迁移、资源加载等)
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default);

    /// <summary>
    /// 启动插件(启动后台服务、订阅事件等)
    /// </summary>
    Task StartAsync(CancellationToken ct = default);

    /// <summary>
    /// 停止插件(清理资源、取消订阅等) 
    /// </summary>
    Task StopAsync(CancellationToken ct = default);
}