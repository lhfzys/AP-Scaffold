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
/// 示例业务插件：演示导航页、设置页、报表数据提供者和简单命令。
/// 新项目可复制本插件作为起点；模板见 templates/ap-plugin。
/// </summary>
[PluginMetadata("AP.Plugin.Sample", Name = "示例业务插件", Version = "1.0.0", Priority = 200, Required = false)]
public class SamplePlugin : PluginBase, INavigationContributor
{
    public SamplePlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        services.Configure<SampleOptions>(configuration.GetSection(SampleOptions.SectionName));

        services.AddTransient<SampleView>();
        services.AddTransient<SampleViewModel>();
        services.AddTransient<SampleSettingsView>();
        services.AddTransient<SampleSettingsViewModel>();

        services.AddSingleton<ISettingsContributor, SampleSettingsContributor>();
        services.AddSingleton<IReportDataProvider, SampleReportDataProvider>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(SampleView).ToString(), typeof(SampleViewModel));
        ViewModelLocationProvider.Register(typeof(SampleSettingsView).ToString(), typeof(SampleSettingsViewModel));

        RegisterSettingsDataTemplate();

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();
        Application.Current.Dispatcher.Invoke(() =>
        {
            regionManager.RegisterViewWithRegion(GlobalConstants.RegionNames.ContentRegion, typeof(SampleView));
        });

        Logger.LogInformation("示例业务插件已加载（导航 + 设置 + 报表示例）");
    }

    public IEnumerable<NavigationMenuItem> GetMenuItems()
    {
        return
        [
            new NavigationMenuItem
            {
                Label = "示例业务",
                IconKind = "Flask",
                NavigationTarget = nameof(SampleView),
                Order = 2000,
                Category = "业务"
            }
        ];
    }

    private static void RegisterSettingsDataTemplate()
    {
        var template = new DataTemplate { DataType = typeof(SampleSettingsViewModel) };
        template.VisualTree = new FrameworkElementFactory(typeof(SampleSettingsView));

        var key = new DataTemplateKey(typeof(SampleSettingsViewModel));
        if (!Application.Current.Resources.Contains(key))
            Application.Current.Resources.Add(key, template);
    }
}
