#region

using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Layout.ViewModels;
using AP.Plugin.Layout.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.Layout;

[PluginMetadata("AP.Plugin.Layout", Name = "系统布局驱动", Version = "1.0.0", Priority = 10)]
public class LayoutPlugin : PluginBase
{
    public LayoutPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<StandardLayoutView>();
        services.AddTransient<SinglePageLayoutView>();

        services.AddTransient<LayoutViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(StandardLayoutView).ToString(), typeof(LayoutViewModel));
        ViewModelLocationProvider.Register(typeof(SinglePageLayoutView).ToString(), typeof(LayoutViewModel));

        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();

        var layoutMode = config["AppConfiguration:LayoutMode"] ?? "Standard";

        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion("MainRegion",
                layoutMode.Equals("SinglePage", StringComparison.OrdinalIgnoreCase)
                    ? typeof(SinglePageLayoutView)
                    : typeof(StandardLayoutView));
        });

        Logger.LogInformation("布局引擎已加载，当前模式: {Mode}", layoutMode);
    }
}