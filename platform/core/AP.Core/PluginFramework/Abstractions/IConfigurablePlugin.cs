using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Core.PluginFramework.Abstractions;

/// <summary>
/// 可配置插件接口(支持服务注册)
/// </summary>
public interface IConfigurablePlugin : IPlugin
{
    /// <summary>
    /// 配置服务(注册到 DI 容器)
    /// </summary>
    void ConfigureServices(IServiceCollection services, IConfiguration configuration);
}