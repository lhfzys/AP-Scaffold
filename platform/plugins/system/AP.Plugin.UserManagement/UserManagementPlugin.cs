using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.UserManagement.ViewModels;
using AP.Plugin.UserManagement.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace AP.Plugin.UserManagement;

/// <summary>
/// 用户管理插件
/// </summary>
[PluginMetadata("AP.Plugin.UserManagement", Name = "用户管理", Version = "1.0.0", Priority = 5, Required = false)]
public class UserManagementPlugin : PluginBase, INavigationContributor
{
    public UserManagementPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<UserListView>();
        services.AddTransient<UserListViewModel>();
        services.AddTransient<UserEditWindow>();
        services.AddTransient<UserEditViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        if (!securityEnabled)
        {
            Logger.LogInformation("安全模块已禁用，跳过用户管理视图注册");
            return;
        }

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("user.manage"))
        {
            Logger.LogInformation("当前用户没有 user.manage 权限，跳过用户管理视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(UserListView).ToString(), typeof(UserListViewModel));
        ViewModelLocationProvider.Register(typeof(UserEditWindow).ToString(), typeof(UserEditViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(UserListView));
        });

        Logger.LogInformation("用户管理插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return new[]
        {
            new NavigationMenuItem
            {
                Label = "用户管理",
                IconKind = "AccountMultiple",
                NavigationTarget = "UserListView",
                Order = 4000,
                Permission = "user.manage"
            }
        };
    }
}
