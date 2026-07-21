using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.DeviceConfiguration.Configuration;
using AP.Plugin.DeviceConfiguration.Models;
using AP.Plugin.DeviceConfiguration.ViewModels;
using AP.Plugin.DeviceConfiguration.Views;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.DeviceConfiguration;

[PluginMetadata("AP.Plugin.DeviceConfiguration", Version = "1.0.0", Name = "设备参数配置", Priority = 100, Required = false)]
public class DeviceConfigurationPlugin : PluginBase
{
    public DeviceConfigurationPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册配置模型绑定
        services.Configure<ScannerConfigModel>(configuration.GetSection(ScannerConfigModel.SectionName));

        // 注册配置编辑视图和 ViewModel
        services.AddTransient<ScannerSettingsView>();
        services.AddTransient<ScannerSettingsViewModel>();

        // 注册配置贡献者
        services.AddSingleton<ISettingsContributor, ScannerSettingsContributor>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        ViewModelLocationProvider.Register(typeof(ScannerSettingsView).ToString(), typeof(ScannerSettingsViewModel));

        // 注册扫码枪配置编辑器的 ViewModel -> View 数据模板
        RegisterEditorDataTemplate();

        Logger.LogInformation("设备参数配置插件已加载");
    }

    private static void RegisterEditorDataTemplate()
    {
        var template = new DataTemplate { DataType = typeof(ScannerSettingsViewModel) };
        template.VisualTree = new FrameworkElementFactory(typeof(ScannerSettingsView));

        var key = new DataTemplateKey(typeof(ScannerSettingsViewModel));
        if (!Application.Current.Resources.Contains(key))
            Application.Current.Resources.Add(key, template);
    }
}
