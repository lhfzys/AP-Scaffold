#region

using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.RoleManagement.ViewModels;
using AP.Plugin.RoleManagement.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.RoleManagement;

[PluginMetadata("AP.Plugin.RoleManagement", Name = "角色权限管理", Version = "1.0.0", Priority = 6, Required = false)]
public class RoleManagementPlugin : PluginBase, INavigationContributor
{
    private bool _enabled = true;

    public RoleManagementPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<RoleListView>();
        services.AddTransient<RoleListViewModel>();
        services.AddTransient<RoleEditWindow>();
        services.AddTransient<RoleEditViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        if (!securityEnabled)
        {
            _enabled = false;
            Logger.LogInformation("安全模块已禁用，跳过角色管理视图注册");
            return;
        }

        // 轻身份模式默认隐藏角色/权限管理：操作员身份 + 审计已足够，
        // 需要完整 RBAC 的项目显式配置 Security:RoleManagement:Enabled=true。
        if (!(configuration.GetValue<bool?>("Security:RoleManagement:Enabled") ?? false))
        {
            _enabled = false;
            Logger.LogInformation("轻身份模式：已隐藏角色管理入口（Security:RoleManagement:Enabled=false）");
            return;
        }

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("role.manage"))
        {
            Logger.LogInformation("当前用户没有 role.manage 权限，跳过角色管理视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(RoleListView).ToString(), typeof(RoleListViewModel));
        ViewModelLocationProvider.Register(typeof(RoleEditWindow).ToString(), typeof(RoleEditViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(RoleListView));
        });

        Logger.LogInformation("角色权限管理插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        if (!_enabled) yield break;

        yield return new NavigationMenuItem
        {
            Label = "角色管理",
            IconKind = "ShieldAccount",
            NavigationTarget = "RoleListView",
            Order = 4100,
            Permission = "role.manage"
        };
    }
}
