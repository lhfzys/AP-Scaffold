using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class PlcConnectionTesterTests
{
    [Fact]
    public async Task TestAsync_ConnectAndRead_ReturnsSuccess()
    {
        var driver = Substitute.For<IPlcService>();
        driver.ConnectAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        driver.IsConnectedAsync().Returns(Task.FromResult(true));
        driver.ReadAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Fake");
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(driver);

        var registry = new PlcDriverRegistry();
        registry.Register(factory);
        var tester = new PlcConnectionTester(
            registry, Substitute.For<IServiceProvider>(), NullLogger<PlcConnectionTester>.Instance);

        var result = await tester.TestAsync(new PlcOptions
        {
            DriverType = "Fake",
            IpAddress = "127.0.0.1",
            Port = 102,
            Timeout = 1000,
            HeartbeatAddress = "DB1.0.0"
        });

        result.Connected.Should().BeTrue();
        result.ReadOk.Should().BeTrue();
        result.ReadValue.Should().Be("True");
    }

    [Fact]
    public async Task TestAsync_ConnectFails_ReturnsFailure()
    {
        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Fake");
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>())
            .Returns(_ => throw new InvalidOperationException("connect failed"));

        var registry = new PlcDriverRegistry();
        registry.Register(factory);
        var tester = new PlcConnectionTester(
            registry, Substitute.For<IServiceProvider>(), NullLogger<PlcConnectionTester>.Instance);

        var result = await tester.TestAsync(new PlcOptions
        {
            DriverType = "Fake",
            Timeout = 1000,
            HeartbeatAddress = "DB1.0.0"
        });

        result.Connected.Should().BeFalse();
        result.Message.Should().Contain("connect failed");
    }

    [Fact]
    public async Task TestAsync_ConnectHangs_TimesOut()
    {
        var driver = Substitute.For<IPlcService>();
        driver.ConnectAsync(Arg.Any<CancellationToken>())
            .Returns(Task.Delay(TimeSpan.FromSeconds(5)));

        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Fake");
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(driver);

        var registry = new PlcDriverRegistry();
        registry.Register(factory);
        var tester = new PlcConnectionTester(
            registry, Substitute.For<IServiceProvider>(), NullLogger<PlcConnectionTester>.Instance);

        var result = await tester.TestAsync(new PlcOptions
        {
            DriverType = "Fake",
            Timeout = 500,
            HeartbeatAddress = "DB1.0.0"
        });

        result.Connected.Should().BeFalse();
        result.Message.Should().Contain("超时");
    }
}
