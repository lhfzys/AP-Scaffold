using AP.Infra.Resilience.Configuration;
using AP.Infra.Resilience.Factories;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly;
using Polly.Registry;
using Xunit;

namespace AP.Infra.Tests.Resilience;

public class ResilienceRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ILoggerFactory>());
        services.AddPlatformResilience(new ConfigurationBuilder().Build());
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPlatformResilience_RegistersDatabasePipeline()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ResiliencePipelineProvider<string>>();

        var pipeline = registry.GetPipeline(ResiliencePipelineFactory.Keys.Database);

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddPlatformResilience_RegistersPlcPipeline()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ResiliencePipelineProvider<string>>();

        var pipeline = registry.GetPipeline(ResiliencePipelineFactory.Keys.Plc);

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddPlatformResilience_RegistersGrpcPipeline()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<ResiliencePipelineProvider<string>>();

        var pipeline = registry.GetPipeline(ResiliencePipelineFactory.Keys.Grpc);

        pipeline.Should().NotBeNull();
    }

    [Fact]
    public void AddPlatformResilience_DoesNotRegisterMisleadingEmptyPipeline()
    {
        using var provider = BuildProvider();

        provider.GetService<ResiliencePipeline>().Should().BeNull();
    }

    [Fact]
    public void Factory_GetPipeline_ResolvesFromRegistry()
    {
        using var provider = BuildProvider();
        var factory = provider.GetRequiredService<ResiliencePipelineFactory>();

        factory.GetPipeline(ResiliencePipelineFactory.Keys.Database).Should().NotBeNull();
        factory.GetPipeline(ResiliencePipelineFactory.Keys.Plc).Should().NotBeNull();
        factory.GetPipeline(ResiliencePipelineFactory.Keys.Grpc).Should().NotBeNull();
    }
}
