using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Hardware.Extensions;

/// <summary>
/// PLC 硬件基础设施 DI 注册扩展。
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册统一 PLC 硬件服务。
    /// 注意：各品牌驱动工厂由各 PLC 插件自行注册（AddSingleton&lt;IPlcDriverFactory, ...&gt;），
    /// <see cref="PlcDriverRegistry"/> 为单例且在首次解析时从 DI 收集所有已注册工厂
    /// （惰性单例保证解析时插件工厂已就位）。
    /// </summary>
    public static IServiceCollection AddPlcHardware(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PlcOptions>(configuration.GetSection(PlcOptions.SectionName));
        services.AddSingleton<PlcDriverRegistry>(sp =>
        {
            var registry = new PlcDriverRegistry();
            foreach (var factory in sp.GetServices<IPlcDriverFactory>())
                registry.Register(factory);
            return registry;
        });
        services.AddSingleton<IPlcService, ActivePlcService>();
        services.AddSingleton<IPlcBatchReadWrite>(sp => (IPlcBatchReadWrite)sp.GetRequiredService<IPlcService>());
        return services;
    }
}
