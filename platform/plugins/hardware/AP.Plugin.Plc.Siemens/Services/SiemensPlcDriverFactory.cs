using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Resilience.Factories;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Plc.Siemens.Services;

/// <summary>
/// 西门子 PLC 驱动工厂。
/// </summary>
public class SiemensPlcDriverFactory : IPlcDriverFactory
{
    public string DriverType => "Siemens";

    public PlcServiceFeatures SupportedFeatures =>
        PlcServiceFeatures.BasicReadWrite |
        PlcServiceFeatures.BatchReadWrite |
        PlcServiceFeatures.AutoReconnect;

    public IPlcService CreateDriver(PlcOptions options, IServiceProvider serviceProvider)
    {
        var siemensOptions = Options.Create(options);
        var logger = serviceProvider.GetRequiredService<ILogger<SiemensPlcService>>();
        var resilienceFactory = serviceProvider.GetRequiredService<ResiliencePipelineFactory>();
        var pipeline = resilienceFactory.GetPipeline(ResiliencePipelineFactory.Keys.Plc);
        var mediator = serviceProvider.GetRequiredService<IMediator>();

        return new SiemensPlcService(siemensOptions, pipeline, logger, mediator);
    }
}
