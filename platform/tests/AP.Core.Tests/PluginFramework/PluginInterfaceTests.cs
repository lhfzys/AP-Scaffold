using AP.Core.PluginFramework.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Core.Tests.PluginFramework;

public class PluginInterfaceTests
{
    [Fact]
    public void IPlugin_IsInterface()
    {
        typeof(IPlugin).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void IPlugin_HasInitializeAsyncMethod()
    {
        var method = typeof(IPlugin).GetMethod("InitializeAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(IServiceProvider));
        parameters[1].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void IPlugin_HasStartAsyncMethod()
    {
        var method = typeof(IPlugin).GetMethod("StartAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public void IPlugin_HasStopAsyncMethod()
    {
        var method = typeof(IPlugin).GetMethod("StopAsync");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(Task));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be(typeof(CancellationToken));
    }

    [Fact]
    public async Task IPlugin_MockCanBeCreated()
    {
        var plugin = Substitute.For<IPlugin>();

        await plugin.InitializeAsync(Substitute.For<IServiceProvider>());
        await plugin.StartAsync();
        await plugin.StopAsync();

        await plugin.Received(1).InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>());
        await plugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await plugin.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IConfigurablePlugin_ExtendsIPlugin()
    {
        typeof(IConfigurablePlugin).IsAssignableTo(typeof(IPlugin)).Should().BeTrue();
    }

    [Fact]
    public void IConfigurablePlugin_HasConfigureServicesMethod()
    {
        var method = typeof(IConfigurablePlugin).GetMethod("ConfigureServices");
        method.Should().NotBeNull();
        method!.ReturnType.Should().Be(typeof(void));
        var parameters = method.GetParameters();
        parameters.Should().HaveCount(2);
        parameters[0].ParameterType.Should().Be(typeof(Microsoft.Extensions.DependencyInjection.IServiceCollection));
        parameters[1].ParameterType.Should().Be(typeof(Microsoft.Extensions.Configuration.IConfiguration));
    }

    [Fact]
    public async Task IConfigurablePlugin_MockSupportsAllMethods()
    {
        var plugin = Substitute.For<IConfigurablePlugin>();

        await plugin.InitializeAsync(Substitute.For<IServiceProvider>());
        await plugin.StartAsync();
        await plugin.StopAsync();

        await plugin.Received(1).InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>());
        await plugin.Received(1).StartAsync(Arg.Any<CancellationToken>());
        await plugin.Received(1).StopAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IApplicationLifecycle_IsInterface()
    {
        typeof(IApplicationLifecycle).IsInterface.Should().BeTrue();
    }
}