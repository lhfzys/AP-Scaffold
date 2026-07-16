using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class PlcDriverRegistryTests
{
    [Fact]
    public void Register_AddFactory_FactoryCanBeRetrieved()
    {
        var registry = new PlcDriverRegistry();
        var factory = CreateFactory("Mitsubishi");

        registry.Register(factory);

        registry.GetFactory("Mitsubishi").Should().Be(factory);
        registry.GetFactory("mitsubishi").Should().Be(factory); // 大小写不敏感
    }

    [Fact]
    public void GetFactory_UnregisteredDriver_ThrowsInvalidOperationException()
    {
        var registry = new PlcDriverRegistry();

        var act = () => registry.GetFactory("Siemens");

        act.Should().Throw<InvalidOperationException>().WithMessage("*未找到 PLC 驱动 'Siemens'*");
    }

    [Fact]
    public void AvailableDrivers_AfterRegister_ReturnsDriverTypes()
    {
        var registry = new PlcDriverRegistry();
        registry.Register(CreateFactory("Siemens"));
        registry.Register(CreateFactory("Mitsubishi"));

        registry.AvailableDrivers.Should().ContainInOrder("Mitsubishi", "Siemens");
    }

    [Fact]
    public void IsRegistered_RegisteredDriver_ReturnsTrue()
    {
        var registry = new PlcDriverRegistry();
        registry.Register(CreateFactory("Omron"));

        registry.IsRegistered("Omron").Should().BeTrue();
        registry.IsRegistered("Siemens").Should().BeFalse();
    }

    private static IPlcDriverFactory CreateFactory(string driverType)
    {
        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns(driverType);
        factory.SupportedFeatures.Returns(PlcServiceFeatures.BasicReadWrite);
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(Substitute.For<IPlcService>());
        return factory;
    }
}
