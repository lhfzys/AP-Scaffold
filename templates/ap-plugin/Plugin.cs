using System.Windows;
using AP.Contracts.Report.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Sample.Configuration;
using AP.Plugin.Sample.Reporting;
using AP.Plugin.Sample.ViewModels;
using AP.Plugin.Sample.Views;
using AP.Shared.PluginSDK.Base;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.PluginSDK.Navigation;
using AP.Shared.Utilities.Constants;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation.Regions;

namespace AP.Plugin.Sample;

/// <summary>
/// 业务插件入口：按需替换 Name / 菜单 / 页面内容。
/// 生成后请把项目加入 AP-Automation.Platform.slnx 并重新构建宿主。
/// </summary>
[PluginMetadata("AP.Plugin.Sample", Name = "业务插件", Version = "1.0.0", Priority = 200, Required = false)]
public class Plugin : PluginBase, INavigationContributor
{
    public Plugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.Configure<PluginOptions>(configuration.GetSection(PluginOptions.SectionName));
        services.AddTransient<PluginView>();
        services.AddTransient<PluginViewModel>();
        services.AddTransient<PluginSettingsView>();
        services.AddTransient<PluginSettingsViewModel>();
        services.AddSingleton<ISettingsContributor, PluginSettingsContributor>();
        services.AddSingleton<IReportDataProvider, PluginReportProvider>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(PluginView).ToString(), typeof(PluginViewModel));
        ViewModelLocationProvider.Register(typeof(PluginSettingsView).ToString(), typeof(PluginSettingsViewModel));

        RegisterSettingsDataTemplate();

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(GlobalConstants.RegionNames.ContentRegion, typeof(PluginView));
        });

        Logger.LogInformation("业务插件已加载");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return
        [
            new NavigationMenuItem
            {
                Label = "业务插件",
                IconKind = "Flask",
                NavigationTarget = nameof(PluginView),
                Order = 2000,
                Category = "业务"
            }
        ];
    }

    private static void RegisterSettingsDataTemplate()
    {
        var template = new DataTemplate { DataType = typeof(PluginSettingsViewModel) };
        template.VisualTree = new FrameworkElementFactory(typeof(PluginSettingsView));

        var key = new DataTemplateKey(typeof(PluginSettingsViewModel));
        if (!Application.Current.Resources.Contains(key))
            Application.Current.Resources.Add(key, template);
    }
}
