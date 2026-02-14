using AP.Contracts.Hardware.Services;
using AP.Core.Capability;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Infra.Resilience.Factories;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using AP.Plugin.Plc.Mitsubishi.Services;
using AP.Shared.PluginSDK.Base;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Plc.Mitsubishi;

[PluginMetadata(
    "AP.Plugin.Plc.Mitsubishi",
    Name = "三菱PLC驱动",
    Version = "1.0.0",
    SupportedRoles = AppRole.Server | AppRole.Standalone,
    Priority = 20
)]
[RequiresCapabilities(PluginCapabilities.Hardware)]
public class MitsubishiPlcPlugin : PluginBase
{
    public MitsubishiPlcPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 1. 绑定配置 (Plugins:Configuration:AP.Plugin.Plc.Mitsubishi)
        // 约定：插件配置都在 Plugins:Configuration:{PluginId} 下
        var configSection = configuration.GetSection(MitsubishiPlcOptions.SectionName);
        services.Configure<MitsubishiPlcOptions>(configSection);

        // 2. 注册服务 (单例，因为我们要维护长连接)
        services.AddSingleton<IPlcService>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<MitsubishiPlcService>>();
            var options = sp.GetRequiredService<IOptions<MitsubishiPlcOptions>>();

            // 获取 Polly 策略工厂
            var resilienceFactory = sp.GetRequiredService<ResiliencePipelineFactory>();
            // 获取名为 "PLC-Retry" 的策略
            var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);

            var mediator = sp.GetRequiredService<IMediator>();

            return new MitsubishiPlcService(options, pipeline, logger, mediator);
        });

        // 注册批量读写接口 (转发给同一个实例)
        services.AddSingleton<IPlcBatchReadWrite>(sp =>
            (IPlcBatchReadWrite)sp.GetRequiredService<IPlcService>());
    }

    public override async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken ct = default)
    {
        await base.InitializeAsync(serviceProvider, ct);
        //_logger.LogInformation("三菱PLC 插件初始化完成 (等待启动)");
        // // 初始化时自动连接
        // try
        // {
        //     var plcService = serviceProvider.GetRequiredService<IPlcService>();
        //     await plcService.ConnectAsync(ct);
        // }
        // catch (Exception ex)
        // {
        //     Logger.LogError(ex, "PLC 初始化连接失败 (将在后台自动重试)");
        //     // 不抛出异常，允许程序继续启动，依靠 Polly 和自动重连机制
        // }
    }

    public override async Task StartAsync(CancellationToken ct = default)
    {
        await base.StartAsync(ct);
        if (ServiceProvider.GetService<IPlcService>() is IPlcService plcService)
            _ = Task.Run(async () =>
            {
                try
                {
                    Logger.LogInformation("🚀 [后台] 开始连接 PLC...");
                    await plcService.ConnectAsync(ct);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "❌ [后台] PLC 连接初始化失败 (Polly 将接管重试)");
                }
            }, ct);
    }

    public override async Task StopAsync(CancellationToken ct = default)
    {
        var plcService = ServiceProvider.GetService<IPlcService>();
        if (plcService != null) await plcService.DisconnectAsync();
        await base.StopAsync(ct);
    }
}