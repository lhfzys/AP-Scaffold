#region

using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.RoleManagement.ViewModels;
using AP.Plugin.RoleManagement.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.RoleManagement;

[PluginMetadata("AP.Plugin.RoleManagement", Name = "角色权限管理", Version = "1.0.0", Priority = 6)]
public class RoleManagementPlugin : PluginBase
{
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
}
