#region

using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.RecipeManagement.ViewModels;
using AP.Plugin.RecipeManagement.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.RecipeManagement;

/// <summary>
/// 配方管理插件
/// </summary>
[PluginMetadata("AP.Plugin.RecipeManagement", Name = "配方管理", Version = "1.0.0", Priority = 8)]
public class RecipeManagementPlugin : PluginBase
{
    public RecipeManagementPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.AddTransient<RecipeListView>();
        services.AddTransient<RecipeListViewModel>();
        services.AddTransient<RecipeEditWindow>();
        services.AddTransient<RecipeEditViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        if (!securityEnabled)
        {
            Logger.LogInformation("安全模块已禁用，跳过配方管理视图注册");
            return;
        }

        var identityService = serviceProvider.GetRequiredService<IIdentityService>();
        if (!identityService.HasPermission("recipe.view"))
        {
            Logger.LogInformation("当前用户没有 recipe.view 权限，跳过配方管理视图注册");
            return;
        }

        ViewModelLocationProvider.Register(typeof(RecipeListView).ToString(), typeof(RecipeListViewModel));
        ViewModelLocationProvider.Register(typeof(RecipeEditWindow).ToString(), typeof(RecipeEditViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(RecipeListView));
        });

        Logger.LogInformation("配方管理插件已加载");
    }
}
