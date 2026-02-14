using AP.Contracts.Hardware.Events;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Dashboard.UI.EventHandlers;
using AP.Plugin.Dashboard.UI.ViewModels;
using AP.Plugin.Dashboard.UI.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.Utilities.Constants;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Windows;
using AP.Contracts.System.Services;

namespace AP.Plugin.Dashboard.UI;

[PluginMetadata(
    "AP.Plugin.Dashboard.UI",
    Name = "仪表盘界面",
    Version = "1.0.0",
    SupportedRoles = AppRole.Client | AppRole.Standalone,
    Priority = 100,
    Dependencies = new[] { "AP.Plugin.Dashboard.Core" }
)]
public class DashboardUIPlugin : PluginBase
{
    public DashboardUIPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);
        services.AddTransient<INotificationHandler<PlcDataChangedEvent>, DashboardPlcHandler>();

        services.AddTransient<INotificationHandler<DeviceConnectingEvent>, DashboardConnectionHandler>();
        services.AddTransient<INotificationHandler<DeviceConnectedEvent>, DashboardConnectionHandler>();
        services.AddTransient<INotificationHandler<DeviceConnectionFailedEvent>, DashboardConnectionHandler>();
        services.AddTransient<INotificationHandler<DeviceDisconnectedEvent>, DashboardConnectionHandler>();

        services.AddTransient<DashboardViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);
        ViewModelLocationProvider.Register<DashboardView, DashboardViewModel>();
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(GlobalConstants.RegionNames.MainRegion, typeof(DashboardView));
        });
    }
}