using Microsoft.Extensions.DependencyInjection;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 单机版策略
/// </summary>
public static class StandaloneBootstrap
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 单机版可能不需要额外的特殊服务，或者注册本地特有的逻辑
    }
}