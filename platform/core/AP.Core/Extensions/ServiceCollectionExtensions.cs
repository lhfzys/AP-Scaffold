using AP.Core.EventBus;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Core.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册核心平台服务
    /// </summary>
    public static IServiceCollection AddPlatformCore(this IServiceCollection services)
    {
        // 注册事件总线
        services.AddTransient<IEventBus, MediatREventBus>();

        // 后续在这里注册 PluginLoader 等核心服务

        return services;
    }
}