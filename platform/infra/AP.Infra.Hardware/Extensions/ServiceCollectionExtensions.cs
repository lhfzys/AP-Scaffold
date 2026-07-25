using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Services;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Infra.Hardware.DeviceRuntime;
using AP.Infra.Hardware.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
        services.AddSingleton<ActivePlcService>();
        // IPlcService 解析为审计装饰器：PLC 写操作自动留痕，业务无感知；
        // 审计/身份服务未注册（如独立测试）时自动降级为不审计
        services.AddSingleton<IPlcService>(sp => new AuditingPlcServiceDecorator(
            sp.GetRequiredService<ActivePlcService>(),
            sp.GetService<IAuditService>(),
            sp.GetService<IIdentityService>(),
            sp.GetService<ILogger<AuditingPlcServiceDecorator>>()));
        services.AddSingleton<IPlcBatchReadWrite>(sp => (IPlcBatchReadWrite)sp.GetRequiredService<IPlcService>());
        // Device Runtime Model：设备注册表 + PLC 设备视图（与 IPlcService 并行的只读视图，不改变现有解析关系）
        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<IDevice>(sp => new PlcDeviceAdapter(
            sp.GetRequiredService<ActivePlcService>(),
            sp.GetRequiredService<IOptions<PlcOptions>>()));
        return services;
    }
}
