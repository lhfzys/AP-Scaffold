
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.DeviceConfiguration.ViewModels;
using AP.Plugin.DeviceConfiguration.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.Utilities.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;

namespace AP.Plugin.DeviceConfiguration;

[PluginMetadata("AP.Plugin.DeviceConfiguration",Version = "1.0.0",Name = "全局设备参数配置UI面板", Priority = 100)]
public class DeviceConfigurationPlugin : PluginBase
{
  

    public DeviceConfigurationPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<ScannerSettingsView>();
        services.AddTransient<ScannerSettingsViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);
        ViewModelLocationProvider.Register(typeof(ScannerSettingsView).ToString(), typeof(ScannerSettingsViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(GlobalConstants.RegionNames.SettingsRegion, typeof(ScannerSettingsView));
            // _regionManager.RegisterViewWithRegion(GlobalConstants.RegionNames.SettingsRegion, typeof(PlcSettingsView));
        });

        Logger.LogInformation("全局设备参数配置UI面板已加载");
    }
}