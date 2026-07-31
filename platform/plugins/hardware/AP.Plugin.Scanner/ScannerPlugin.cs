using AP.Contracts.Hardware.Services;
using AP.Core.Capability;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Scanner.Configuration;
using AP.Plugin.Scanner.Services;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;


namespace AP.Plugin.Scanner;

[PluginMetadata(
    "AP.Plugin.Scanner",
    Name = "串口扫码枪驱动",
    Version = "1.0.0",
    SupportedRoles = AppRole.Client | AppRole.Standalone,
    Priority = 20,
    Required = false
)]
[RequiresCapabilities(PluginCapabilities.AccessSerialPort | PluginCapabilities.PublishEvents)]
public class ScannerPlugin : PluginBase
{
    public ScannerPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 无扫码枪的项目可整体禁用：不注册服务与设备视图（状态栏/设备注册表均不出现）
        if (!configuration.GetValue($"{SerialPortOptions.SectionName}:Enabled", true))
        {
            Logger.LogInformation("扫码枪已禁用（{Section}:Enabled=false），跳过服务注册", SerialPortOptions.SectionName);
            return;
        }

        var configSection = configuration.GetSection(SerialPortOptions.SectionName);
        services.Configure<SerialPortOptions>(configSection);

        // 注册单例服务
        services.AddSingleton<IScannerService, SerialPortScannerService>();
        // Device Runtime Model：扫码枪的设备视图（Bootstrapper 统一登记进设备注册表）
        services.AddSingleton<AP.Contracts.Hardware.DeviceRuntime.IDevice>(
            sp => (AP.Contracts.Hardware.DeviceRuntime.IDevice)sp.GetRequiredService<IScannerService>());
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        // Enabled=false 时服务未注册，直接跳过（不开口、不报初始化失败）
        var scanner = serviceProvider.GetService<IScannerService>();
        if (scanner == null)
        {
            Logger.LogInformation("扫码枪未注册（已禁用），跳过启动");
            return;
        }

        try
        {
            await scanner.OpenAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "扫码枪初始化失败，请检查 COM 口配置");
        }
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        if (ServiceProvider != null)
        {
            var scanner = ServiceProvider.GetService<IScannerService>();
            if (scanner != null) await scanner.CloseAsync();
        }

        await base.StopAsync(ct);
    }
}