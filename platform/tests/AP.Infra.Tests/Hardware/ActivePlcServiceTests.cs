using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class ActivePlcServiceTests
{
    [Fact]
    public void Constructor_WithRegisteredDriver_SelectsCorrectDriver()
    {
        var options = Options.Create(new PlcOptions { DriverType = "Siemens" });
        var registry = new PlcDriverRegistry();
        var siemensFactory = CreateFactory("Siemens");
        var mitsubishiFactory = CreateFactory("Mitsubishi");
        registry.Register(siemensFactory);
        registry.Register(mitsubishiFactory);

        var service = new ActivePlcService(
            options,
            registry,
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<ActivePlcService>>());

        // 访问 SupportedFeatures 触发内部驱动创建
        var features = service.SupportedFeatures;

        features.Should().Be(PlcServiceFeatures.BasicReadWrite);
        siemensFactory.Received(1).CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>());
        mitsubishiFactory.DidNotReceive().CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>());
    }

    [Fact]
    public void Constructor_WithUnregisteredDriver_ThrowsOnFirstAccess()
    {
        var options = Options.Create(new PlcOptions { DriverType = "Omron" });
        var registry = new PlcDriverRegistry();
        var service = new ActivePlcService(
            options,
            registry,
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<ActivePlcService>>());

        var act = () => service.SupportedFeatures;

        act.Should().Throw<InvalidOperationException>().WithMessage("*未找到 PLC 驱动 'Omron'*");
    }

    private static IPlcDriverFactory CreateFactory(string driverType)
    {
        var factory = Substitute.For<IPlcDriverFactory>();
        var service = Substitute.For<IPlcService>();
        service.SupportedFeatures.Returns(PlcServiceFeatures.BasicReadWrite);

        factory.DriverType.Returns(driverType);
        factory.SupportedFeatures.Returns(PlcServiceFeatures.BasicReadWrite);
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(service);
        return factory;
    }
}
