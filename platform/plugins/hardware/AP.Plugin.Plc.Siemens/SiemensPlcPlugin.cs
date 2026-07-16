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

namespace AP.Plugin.Plc.Siemens;

[PluginMetadata(
    "AP.Plugin.Plc.Siemens",
    Name = "西门子PLC驱动",
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 21
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
    }

    public override async Task StartAsync(CancellationToken ct = default)
    {
        await base.StartAsync(ct);

        if (ServiceProvider.GetService<IPlcService>() is IPlcService plcService)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInformation("🚀 [后台] 开始连接西门子 PLC...");
                    await plcService.ConnectAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "❌ [后台] 西门子 PLC 连接初始化失败");
                }
            }, ct);
        }
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        var plcService = ServiceProvider.GetService<IPlcService>();
        if (plcService != null) await plcService.DisconnectAsync();
        await base.StopAsync(ct);
    }
}
