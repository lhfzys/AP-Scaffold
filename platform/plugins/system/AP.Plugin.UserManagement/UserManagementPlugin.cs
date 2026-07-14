using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.UserManagement.ViewModels;
using AP.Plugin.UserManagement.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace AP.Plugin.UserManagement;

/// <summary>
/// 用户管理插件
/// </summary>
[PluginMetadata("AP.Plugin.UserManagement", Name = "用户管理", Version = "1.0.0", Priority = 5)]
public class UserManagementPlugin : PluginBase
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
}
