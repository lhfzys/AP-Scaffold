using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Resilience.Factories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Plc.Omron.Services;

/// <summary>
/// 欧姆龙 PLC 驱动工厂。
/// </summary>
public class OmronPlcDriverFactory : IPlcDriverFactory
{
    public string DriverType => "Omron";

    public PlcServiceFeatures SupportedFeatures =>
        PlcServiceFeatures.BasicReadWrite |
        PlcServiceFeatures.BatchReadWrite |
        PlcServiceFeatures.AutoReconnect;

    public IPlcService CreateDriver(PlcOptions options, IServiceProvider serviceProvider)
    {
        var omronOptions = Options.Create(options);
        var logger = serviceProvider.GetRequiredService<ILogger<OmronPlcService>>();
        var resilienceFactory = serviceProvider.GetRequiredService<ResiliencePipelineFactory>();
        var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        return new OmronPlcService(omronOptions, pipeline, logger, mediator);
    }
}
