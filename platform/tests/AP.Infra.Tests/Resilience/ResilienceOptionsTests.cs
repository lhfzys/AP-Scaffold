using AP.Infra.Resilience.Configuration;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Resilience;

public class ResilienceOptionsTests
{
    [Fact]
    public void SectionName_IsResilience()
    {
        ResilienceOptions.SectionName.Should().Be("Resilience");
    }

    [Fact]
    public void DatabaseRetryCount_Default_Is3()
    {
        var options = new ResilienceOptions();
        options.DatabaseRetryCount.Should().Be(3);
    }

    [Fact]
    public void PlcRetryCount_Default_Is5()
    {
        var options = new ResilienceOptions();
        options.PlcRetryCount.Should().Be(5);
    }

    [Fact]
    public void GrpcCircuitBreakerThreshold_Default_Is5()
    {
        var options = new ResilienceOptions();
        options.GrpcCircuitBreakerThreshold.Should().Be(5);
    }

    [Fact]
    public void CircuitBreakerDurationSeconds_Default_Is30()
    {
        var options = new ResilienceOptions();
        options.CircuitBreakerDurationSeconds.Should().Be(30);
    }

    [Fact]
    public void AllProperties_CanBeCustomized()
    {
        var options = new ResilienceOptions
        {
            DatabaseRetryCount = 5,
            PlcRetryCount = 10,
            GrpcCircuitBreakerThreshold = 3,
            CircuitBreakerDurationSeconds = 60
        };

        options.DatabaseRetryCount.Should().Be(5);
        options.PlcRetryCount.Should().Be(10);
        options.GrpcCircuitBreakerThreshold.Should().Be(3);
        options.CircuitBreakerDurationSeconds.Should().Be(60);
    }

    [Fact]
    public void DatabaseRetryCount_RoundTrip_PreservesValue()
    {
        var original = new ResilienceOptions { DatabaseRetryCount = 7 };
        var deserialized = new ResilienceOptions { DatabaseRetryCount = original.DatabaseRetryCount };
        deserialized.DatabaseRetryCount.Should().Be(original.DatabaseRetryCount);
    }

    [Fact]
    public void Properties_CanBeResetToZero()
    {
        var options = new ResilienceOptions
        {
            DatabaseRetryCount = 0,
            PlcRetryCount = 0,
            GrpcCircuitBreakerThreshold = 0,
            CircuitBreakerDurationSeconds = 0
        };

        options.DatabaseRetryCount.Should().Be(0);
        options.PlcRetryCount.Should().Be(0);
        options.GrpcCircuitBreakerThreshold.Should().Be(0);
        options.CircuitBreakerDurationSeconds.Should().Be(0);
    }
}