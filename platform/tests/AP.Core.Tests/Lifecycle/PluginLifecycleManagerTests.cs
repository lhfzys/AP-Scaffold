using AP.Core.Lifecycle;
using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Core.PluginFramework.Loading;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Core.Tests.Lifecycle;

public class PluginLifecycleManagerTests
{
    private readonly ILogger<PluginLifecycleManager> _logger = Substitute.For<ILogger<PluginLifecycleManager>>();
    private readonly PluginLifecycleManager _manager;

    public PluginLifecycleManagerTests()
    {
        _manager = new PluginLifecycleManager(_logger);
    }

    [Fact]
    public void Constructor_InitializesEmptyState()
    {
        _manager.GetLoadedPlugins().Should().BeEmpty();
        _manager.GetRunningPlugins().Should().BeEmpty();
        _manager.GetFailedPlugins().Should().BeEmpty();
    }

    [Fact]
    public void RegisterPlugins_NonLoadedPlugins_AreSkipped()
    {
        var descriptor = CreateDescriptor("AP.Plugin.Skip", isLoaded: false, instance: null);

        _manager.RegisterPlugins(new[] { descriptor });

        _manager.GetLoadedPlugins().Should().BeEmpty();
    }

    [Fact]
    public void RegisterPlugins_LoadedPlugin_IsAdded()
    {
        var instance = Substitute.For<IPlugin>();
        var descriptor = CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance);

        _manager.RegisterPlugins(new[] { descriptor });

        _manager.GetLoadedPlugins().Should().ContainSingle()
            .Which.Metadata.Id.Should().Be("AP.Plugin.Test");
    }

    [Fact]
    public void RegisterPlugins_MultiplePlugins_AllAdded()
    {
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.One", isLoaded: true, instance: Substitute.For<IPlugin>()),
            CreateDescriptor("AP.Plugin.Two", isLoaded: true, instance: Substitute.For<IPlugin>()),
        };

        _manager.RegisterPlugins(descriptors);

        _manager.GetLoadedPlugins().Should().HaveCount(2);
    }

    [Fact]
    public void GetPluginState_LoadedPlugin_ReturnsLoaded()
    {
        var instance = Substitute.For<IPlugin>();
        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });

        var state = _manager.GetPluginState("AP.Plugin.Test");

        state.Should().Be(AP.Core.StateMachine.PluginState.Loaded);
    }

    [Fact]
    public void GetPluginState_UnknownPlugin_ReturnsNull()
    {
        var state = _manager.GetPluginState("AP.Plugin.Nonexistent");
        state.Should().BeNull();
    }

    [Fact]
    public async Task InitializePluginsAsync_PluginInitialized_Successfully()
    {
        var instance = Substitute.For<IPlugin>();
        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });

        await _manager.InitializePluginsAsync(Substitute.For<IServiceProvider>());

        await instance.Received(1).InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InitializePluginsAsync_PluginFails_StateIsFailed()
    {
        var instance = Substitute.For<IPlugin>();
        instance.InitializeAsync(Arg.Any<IServiceProvider>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Init failed")));

        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });

        await _manager.InitializePluginsAsync(Substitute.For<IServiceProvider>());

        var state = _manager.GetPluginState("AP.Plugin.Test");
        state.Should().Be(AP.Core.StateMachine.PluginState.Failed);
        _manager.GetFailedPlugins().Should().ContainSingle();
    }

    [Fact]
    public async Task StartStopPluginsAsync_FullLifecycle_Succeeds()
    {
        var instance = Substitute.For<IPlugin>();
        var svp = Substitute.For<IServiceProvider>();
        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });

        await _manager.InitializePluginsAsync(svp);
        await _manager.StartPluginsAsync();

        var runningState = _manager.GetPluginState("AP.Plugin.Test");
        runningState.Should().Be(AP.Core.StateMachine.PluginState.Running);
        _manager.GetRunningPlugins().Should().ContainSingle();

        await _manager.StopPluginsAsync();

        var stoppedState = _manager.GetPluginState("AP.Plugin.Test");
        stoppedState.Should().Be(AP.Core.StateMachine.PluginState.Stopped);
    }

    [Fact]
    public async Task StartPluginsAsync_PluginFails_StateIsFailed()
    {
        var instance = Substitute.For<IPlugin>();
        instance.StartAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("Start failed")));

        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });
        await _manager.InitializePluginsAsync(Substitute.For<IServiceProvider>());

        await _manager.StartPluginsAsync();

        var state = _manager.GetPluginState("AP.Plugin.Test");
        state.Should().Be(AP.Core.StateMachine.PluginState.Failed);
        _manager.GetFailedPlugins().Should().ContainSingle();
    }

    [Fact]
    public async Task StopPluginsAsync_RunningPlugins_AreStopped()
    {
        var instance = Substitute.For<IPlugin>();
        var svp = Substitute.For<IServiceProvider>();
        _manager.RegisterPlugins(new[] { CreateDescriptor("AP.Plugin.Test", isLoaded: true, instance: instance) });
        await _manager.InitializePluginsAsync(svp);
        await _manager.StartPluginsAsync();

        await _manager.StopPluginsAsync();

        await instance.Received(1).StopAsync(Arg.Any<CancellationToken>());
        _manager.GetRunningPlugins().Should().BeEmpty();
    }

    [Fact]
    public void RegisterPlugins_PluginsSortedByPriority_TheFirstHasLowerPriority()
    {
        var lowPri = CreateDescriptor("AP.Plugin.Low", isLoaded: true, instance: Substitute.For<IPlugin>(), priority: 10);
        var highPri = CreateDescriptor("AP.Plugin.High", isLoaded: true, instance: Substitute.For<IPlugin>(), priority: 100);

        _manager.RegisterPlugins(new[] { highPri, lowPri });

        var loaded = _manager.GetLoadedPlugins();
        loaded[0].Metadata.Priority.Should().BeLessThanOrEqualTo(loaded[1].Metadata.Priority);
    }

    private static PluginDescriptor CreateDescriptor(
        string id,
        bool isLoaded,
        IPlugin? instance,
        int priority = 100)
    {
        var metadata = new PluginMetadataAttribute(id)
        {
            Name = $"Test Plugin {id}",
            Version = "1.0.0",
            Priority = priority,
        };
        var descriptor = new PluginDescriptor(
            metadata,
            typeof(IPlugin),
            null!, // PluginLoadContext - null for testing
            typeof(IPlugin).Assembly);

        descriptor.IsLoaded = isLoaded;
        descriptor.Instance = instance;
        return descriptor;
    }
}