using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Extensions;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class PlcHardwareRegistrationTests
{
    [Fact]
    public void AddPlcHardware_RegistryIsPopulatedFromDiRegisteredFactories()
    {
        // 复现现场缺陷：注册表曾恒为空，GetFactory 抛 "已注册的驱动: 无"
        var mitsubishiFactory = Substitute.For<IPlcDriverFactory>();
        mitsubishiFactory.DriverType.Returns("Mitsubishi");
        var siemensFactory = Substitute.For<IPlcDriverFactory>();
        siemensFactory.DriverType.Returns("Siemens");

        var services = new ServiceCollection();
        // 模拟 PLC 插件在 ConfigureServices 中注册驱动工厂
        services.AddSingleton<IPlcDriverFactory>(mitsubishiFactory);
        services.AddSingleton<IPlcDriverFactory>(siemensFactory);
        services.AddPlcHardware(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<PlcDriverRegistry>();

        registry.IsRegistered("Mitsubishi").Should().BeTrue();
        registry.IsRegistered("Siemens").Should().BeTrue();
        registry.AvailableDrivers.Should().BeEquivalentTo("Mitsubishi", "Siemens");
    }

    [Fact]
    public void AddPlcHardware_ResolvesAuditingDecoratorAsPlcService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<Microsoft.Extensions.Logging.ILogger<ActivePlcService>>());
        services.AddPlcHardware(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        // IPlcService 解析为审计装饰器，ActivePlcService 作为其内部代理单独注册
        provider.GetRequiredService<IPlcService>().Should().BeOfType<AuditingPlcServiceDecorator>();
        provider.GetRequiredService<ActivePlcService>().Should().NotBeNull();
        // IPlcBatchReadWrite 与 IPlcService 为同一装饰器实例
        provider.GetRequiredService<IPlcBatchReadWrite>().Should().BeSameAs(provider.GetRequiredService<IPlcService>());
    }
}
