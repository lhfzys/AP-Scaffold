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
        // 带类型批量读取（驱动逐个接入，未接入的驱动经此调用抛 NotSupportedException）
        services.AddSingleton<IPlcTypedBatchRead>(sp => sp.GetRequiredService<ActivePlcService>());
        // Device Runtime Model：设备注册表 + PLC 设备视图（与 IPlcService 并行的只读视图，不改变现有解析关系）
        services.AddSingleton<IDeviceRegistry, DeviceRegistry>();
        services.AddSingleton<IDevice>(sp => new PlcDeviceAdapter(
            sp.GetRequiredService<ActivePlcService>(),
            sp.GetRequiredService<IOptions<PlcOptions>>()));
        // 点表：启动时加载并校验（快速失败），地址验证器由各驱动插件注册
        services.AddSingleton<ITagTable>(sp => new TagTable(
            sp.GetRequiredService<IDeviceRegistry>(),
            sp.GetServices<IAddressValidator>(),
            Path.Combine(AppContext.BaseDirectory, "Configuration", "tags.json")));
        // Tag 服务：业务按点名读写的唯一入口
        services.AddSingleton<ITagService, TagService>();
        // 采集引擎与最新值表（启动/停止由 Bootstrapper 显式调用）
        services.AddSingleton<LatestTagValueStore>();
        services.AddSingleton<TagAcquisitionEngine>(sp =>
        {
            var table = (TagTable)sp.GetRequiredService<ITagTable>();
            return new TagAcquisitionEngine(
                table,
                table.Acquisition,
                sp.GetRequiredService<ITagService>(),
                sp.GetRequiredService<IPlcTypedBatchRead>(),
                sp.GetRequiredService<IDeviceRegistry>(),
                sp.GetRequiredService<LatestTagValueStore>(),
                sp.GetRequiredService<ILogger<TagAcquisitionEngine>>());
        });
        return services;
    }
}
