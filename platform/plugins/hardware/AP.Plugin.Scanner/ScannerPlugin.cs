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
    Priority = 20
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

        var configSection = configuration.GetSection(SerialPortOptions.SectionName);
        services.Configure<SerialPortOptions>(configSection);

        // 注册单例服务
        services.AddSingleton<IScannerService, SerialPortScannerService>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        try
        {
            var scanner = serviceProvider.GetRequiredService<IScannerService>();
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