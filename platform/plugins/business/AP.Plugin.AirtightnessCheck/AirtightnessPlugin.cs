#region

using System.Windows;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.AirtightnessCheck.Configuration;
using AP.Plugin.AirtightnessCheck.ViewModels;
using AP.Plugin.AirtightnessCheck.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.AirtightnessCheck;

[PluginMetadata("AP.Plugin.AirtightnessCheck", Name = "气密性检测业务", Version = "1.0.0", Priority = 30)]
public class AirtightnessPlugin : PluginBase
{
    private const string TargetRegion = "ContentRegion";

    public AirtightnessPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 绑定配置
        var configSection = configuration.GetSection(AirtightnessOptions.SectionName);
        services.Configure<AirtightnessOptions>(configSection);

        services.AddTransient<AirtightnessView>();
        services.AddTransient<AirtightnessViewModel>();
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);

        // 手动注册 ViewModel 映射
        ViewModelLocationProvider.Register(typeof(AirtightnessView).ToString(), typeof(AirtightnessViewModel));

        var regionManager = serviceProvider.GetRequiredService<IRegionManager>();

        Application.Current.Dispatcher.Invoke(() =>
        {
            // 将业务界面挂载到 SinglePageLayout 提供的内容槽中
            regionManager.RegisterViewWithRegion(TargetRegion, typeof(AirtightnessView));
        });
    }
}