using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Resilience.Factories;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using IoTClient.Clients.PLC;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace AP.Plugin.Plc.Mitsubishi.Services;

/// <summary>
/// 三菱 PLC 驱动工厂。
/// </summary>
public class MitsubishiPlcDriverFactory : IPlcDriverFactory
{
    public string DriverType => "Mitsubishi";

    public PlcServiceFeatures SupportedFeatures =>
        PlcServiceFeatures.BasicReadWrite |
        PlcServiceFeatures.BatchReadWrite |
        PlcServiceFeatures.AutoReconnect;

    public IPlcService CreateDriver(PlcOptions options, IServiceProvider serviceProvider)
    {
        var mitsubishiOptions = Options.Create(new MitsubishiPlcOptions
        {
            IpAddress = options.IpAddress,
            Port = options.Port,
            Timeout = options.Timeout,
            Version = options.Model,
            HeartbeatAddress = options.HeartbeatAddress,
            HeartbeatIntervalSeconds = options.HeartbeatIntervalSeconds,
            ReconnectBackoffSeconds = options.ReconnectBackoffSeconds,
            SupervisorRestartDelaySeconds = options.SupervisorRestartDelaySeconds
        });

        var logger = serviceProvider.GetRequiredService<ILogger<MitsubishiPlcService>>();
        var resilienceFactory = serviceProvider.GetRequiredService<ResiliencePipelineFactory>();
        var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        return new MitsubishiPlcService(mitsubishiOptions, pipeline, logger, mediator);
    }
}
