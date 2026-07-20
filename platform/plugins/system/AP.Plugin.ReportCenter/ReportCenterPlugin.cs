#region

using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.ReportCenter.ViewModels;
using AP.Plugin.ReportCenter.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.ReportCenter;

/// <summary>
/// 报表中心插件
/// </summary>
[PluginMetadata("AP.Plugin.ReportCenter", Name = "报表中心", Version = "1.0.0", Priority = 9)]
public class ReportCenterPlugin : PluginBase, INavigationContributor
{
    public ReportCenterPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<ReportListView>();
        services.AddTransient<ReportListViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("report.view"))
        {
            Logger.LogInformation("当前用户没有 report.view 权限，跳过报表中心视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(ReportListView).ToString(), typeof(ReportListViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(ReportListView));
        });

        Logger.LogInformation("报表中心插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return new[]
        {
            new NavigationMenuItem
            {
                Label = "报表中心",
                IconKind = "FileChartOutline",
                NavigationTarget = "ReportListView",
                Order = 3000,
                Permission = "report.view"
            }
        };
    }
}
