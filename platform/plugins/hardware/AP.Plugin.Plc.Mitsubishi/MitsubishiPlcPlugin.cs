using AP.Contracts.Hardware.Services;
using AP.Core.Capability;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Plc.Mitsubishi.Services;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.Plc.Mitsubishi;

[PluginMetadata(
    "AP.Plugin.Plc.Mitsubishi",
    Name = "三菱PLC驱动",
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 20
)]
[RequiresCapabilities(PluginCapabilities.Hardware)]
public class MitsubishiPlcPlugin : PluginBase
{
    public MitsubishiPlcPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册三菱 PLC 驱动工厂
        // 统一的 IPlcService 由 AP.Infra.Hardware.ActivePlcService 根据 Plc:DriverType 转发
        services.AddSingleton<IPlcDriverFactory, MitsubishiPlcDriverFactory>();
    }

    public override async Task StartAsync(CancellationToken ct = default)
    {
        await base.StartAsync(ct);
        if (ServiceProvider.GetService<IPlcService>() is IPlcService plcService)
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInformation("🚀 [后台] 开始连接 PLC...");
                    await plcService.ConnectAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "❌ [后台] PLC 连接初始化失败 (Polly 将接管重试)");
                }
            }, ct);
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        var plcService = ServiceProvider.GetService<IPlcService>();
        if (plcService != null) await plcService.DisconnectAsync();
        await base.StopAsync(ct);
    }
}
