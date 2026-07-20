#region

using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.AuditLog.ViewModels;
using AP.Plugin.AuditLog.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.AuditLog;

/// <summary>
/// 审计日志插件
/// </summary>
[PluginMetadata("AP.Plugin.AuditLog", Name = "审计日志", Version = "1.0.0", Priority = 7)]
public class AuditLogPlugin : PluginBase, INavigationContributor
{
    public AuditLogPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<AuditLogListView>();
        services.AddTransient<AuditLogListViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        if (!securityEnabled)
        {
            Logger.LogInformation("安全模块已禁用，跳过审计日志视图注册");
            return;
        }

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("audit.view"))
        {
            Logger.LogInformation("当前用户没有 audit.view 权限，跳过审计日志视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(AuditLogListView).ToString(), typeof(AuditLogListViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(AuditLogListView));
        });

        Logger.LogInformation("审计日志插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return new[]
        {
            new NavigationMenuItem
            {
                Label = "审计日志",
                IconKind = "ClipboardTextClock",
                NavigationTarget = "AuditLogListView",
                Order = 4200,
                Permission = "audit.view"
            }
        };
    }
}
