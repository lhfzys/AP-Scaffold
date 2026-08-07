#region

using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.TagConfiguration.ViewModels;
using AP.Plugin.TagConfiguration.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Navigation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.TagConfiguration;

/// <summary>
/// 点表配置插件（tags.json 可视化编辑，保存后热重载即时生效）
/// </summary>
[PluginMetadata("AP.Plugin.TagConfiguration", Name = "点表配置", Version = "1.0.0", Priority = 100, Required = false)]
public class TagConfigurationPlugin : PluginBase, INavigationContributor
{
    public TagConfigurationPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<TagTableListView>();
        services.AddTransient<TagTableListViewModel>();
        services.AddTransient<TagEditWindow>();
        services.AddTransient<TagEditDialogViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("device.config"))
        {
            Logger.LogInformation("当前用户没有 device.config 权限，跳过点表配置视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(TagTableListView).ToString(), typeof(TagTableListViewModel));
        ViewModelLocationProvider.Register(typeof(TagEditWindow).ToString(), typeof(TagEditDialogViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(TagTableListView));
        });

        Logger.LogInformation("点表配置插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return new[]
        {
            new NavigationMenuItem
            {
                Label = "点表配置",
                IconKind = "TableEdit",
                NavigationTarget = "TagTableListView",
                Order = 1100,
                Permission = "device.config"
            }
        };
    }
}
