namespace AP.Core.PluginFramework.Abstractions;

/// <summary>
/// 应用生命周期接口 (Host 实现，插件可注入使用)
/// </summary>
public interface IApplicationLifecycle
{
    /// <summary>
    /// 应用已启动
    /// </summary>
    CancellationToken ApplicationStarted { get; }

    /// <summary>
    /// 应用正在停止
    /// </summary>
    CancellationToken ApplicationStopping { get; }

    /// <summary>
    /// 应用已停止
    /// </summary>
    CancellationToken ApplicationStopped { get; }

    /// <summary>
    /// 请求停止应用
    /// </summary>
    void StopApplication();
}