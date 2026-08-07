#region

using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Layout.ViewModels;
using AP.Plugin.Layout.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.Layout;

[PluginMetadata("AP.Plugin.Layout", Name = "系统布局驱动", Version = "1.0.0", Priority = 10)]
public class LayoutPlugin : PluginBase, INavigationContributor
{
    public LayoutPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<StandardLayoutView>();
        services.AddTransient<SinglePageLayoutView>();
        services.AddTransient<HeaderView>();
        services.AddTransient<SidebarView>();
        services.AddTransient<DashboardView>();

        services.AddTransient<LayoutViewModel>();
        services.AddTransient<SidebarViewModel>();
        services.AddTransient<DashboardViewModel>();

        // 状态栏系统监控（CPU/内存），契约在 AP.Contracts.System
        services.AddSingleton<AP.Contracts.System.Services.ISystemMonitorService, Services.SystemMonitorService>();
        // 数据库连通探测（状态栏与首页服务状态卡共用）
        services.AddSingleton<Services.DatabaseStatusService>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(StandardLayoutView).ToString(), typeof(LayoutViewModel));
        ViewModelLocationProvider.Register(typeof(SinglePageLayoutView).ToString(), typeof(LayoutViewModel));
        ViewModelLocationProvider.Register(typeof(SidebarView).ToString(), typeof(SidebarViewModel));
        ViewModelLocationProvider.Register(typeof(DashboardView).ToString(), typeof(DashboardViewModel));

        var config = serviceProvider.GetRequiredService<IConfiguration>();
        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();

        var layoutMode = config["AppConfiguration:LayoutMode"] ?? "Standard";

        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion("MainRegion",
                layoutMode.Equals("SinglePage", StringComparison.OrdinalIgnoreCase)
                    ? typeof(SinglePageLayoutView)
                    : typeof(StandardLayoutView));

            // 注册仪表板视图为默认首页
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(DashboardView));
        });

        Logger.LogInformation("布局引擎已加载，当前模式: {Mode}", layoutMode);
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return new[]
        {
            new NavigationMenuItem
            {
                Label = "仪表板",
                IconKind = "ViewDashboard",
                NavigationTarget = "DashboardView",
                Order = 100,
                IsDefault = true
            }
        };
    }
}