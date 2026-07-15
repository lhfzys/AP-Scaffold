using System.Windows;
using AP.Contracts.System.Services;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.SystemSettings.Configuration;
using AP.Plugin.SystemSettings.Editors;
using AP.Plugin.SystemSettings.Services;
using AP.Plugin.SystemSettings.ViewModels;
using AP.Plugin.SystemSettings.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace AP.Plugin.SystemSettings;

[PluginMetadata("AP.Plugin.SystemSettings", Name = "系统配置中心", Version = "1.0.0", Priority = 5)]
public class SystemSettingsPlugin : PluginBase
{
    public SystemSettingsPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册配置编辑器
        services.AddTransient<AppConfigurationEditorViewModel>();

        // 注册配置贡献者
        services.AddSingleton<ISettingsContributor, AppConfigurationContributor>();

        // 注册配置框架视图和 ViewModel
        services.AddTransient<SettingsShellView>();
        services.AddTransient<SettingsShellViewModel>();
        services.AddTransient<SettingsDialogWindow>();

        // 注册配置服务
        services.AddSingleton<SettingsService>();
        services.AddSingleton<ISettingsDialogService, SettingsDialogService>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(SettingsShellView).ToString(), typeof(SettingsShellViewModel));

        // 注册 ViewModel -> View 的数据模板，供配置中心动态渲染编辑器
        RegisterEditorDataTemplates();

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.SettingsRegion,
                typeof(SettingsShellView));

            // 同时作为普通内容视图注册到 ContentRegion，支持在右侧内容区显示
            regionManager.RegisterViewWithRegion(
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                typeof(SettingsShellView));
        });

        Logger.LogInformation("系统配置中心已加载");
    }

    private static void RegisterEditorDataTemplates()
    {
        RegisterDataTemplate<AppConfigurationEditorViewModel, AppConfigurationEditorView>();
    }

    private static void RegisterDataTemplate<TViewModel, TView>()
        where TViewModel : class
        where TView : FrameworkElement, new()
    {
        var template = new DataTemplate { DataType = typeof(TViewModel) };
        template.VisualTree = new FrameworkElementFactory(typeof(TView));

        var key = new DataTemplateKey(typeof(TViewModel));
        if (!Application.Current.Resources.Contains(key))
            Application.Current.Resources.Add(key, template);
    }
}
