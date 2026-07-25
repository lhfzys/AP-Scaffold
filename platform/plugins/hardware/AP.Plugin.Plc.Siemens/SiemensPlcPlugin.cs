using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Plc.Siemens.Configuration;
using AP.Plugin.Plc.Siemens.Services;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Plc.Siemens;

[PluginMetadata(
    "AP.Plugin.Plc.Siemens",
    Name = "西门子PLC驱动",
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 21,
    Required = false
)]
public class SiemensPlcPlugin : PluginBase
{
    public SiemensPlcPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册西门子 PLC 驱动工厂
        services.AddSingleton<IPlcDriverFactory, SiemensPlcDriverFactory>();
        // 地址验证器：点表加载时校验/解析西门子地址（Address Object 缓存于 ResolvedTag）
        services.AddSingleton<AP.Contracts.Hardware.DeviceRuntime.IAddressValidator, Addressing.SiemensAddressValidator>();
    }

    public override async Task StartAsync(CancellationToken ct = default)
    {
        await base.StartAsync(ct);

        // 多品牌插件共存时，只有配置激活本品牌的插件才发起连接，
        // 避免对同一个 ActivePlcService 代理重复连接
        if (!IsActiveDriver())
        {
            Logger.LogInformation("当前激活驱动非西门子，跳过 PLC 连接");
            return;
        }

        if (ServiceProvider.GetService<IPlcService>() is IPlcService plcService)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInformation("后台任务开始连接 PLC");
                    await plcService.ConnectAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "PLC 连接初始化失败");
                }
            }, ct);
        }
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        // 与 StartAsync 对应：只有本品牌发起过连接才负责断开
        if (IsActiveDriver())
        {
            var plcService = ServiceProvider.GetService<IPlcService>();
            if (plcService != null) await plcService.DisconnectAsync();
        }
        await base.StopAsync(ct);
    }

    /// <summary>
    /// 当前配置激活的驱动是否为本插件品牌（与 SiemensPlcDriverFactory.DriverType 一致）
    /// </summary>
    private bool IsActiveDriver()
    {
        var driverType = ServiceProvider.GetService<IOptions<PlcOptions>>()?.Value.DriverType;
        return string.Equals(driverType, "Siemens", StringComparison.OrdinalIgnoreCase);
    }
}
