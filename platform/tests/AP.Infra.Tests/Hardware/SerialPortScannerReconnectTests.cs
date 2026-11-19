using AP.Contracts.Hardware.DeviceRuntime;
using AP.Plugin.Scanner.Configuration;
using AP.Plugin.Scanner.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class SerialPortScannerReconnectTests
{
    [Fact]
    public async Task OpenAsync_FirstOpenFails_StartsSupervisorAndDoesNotThrow()
    {
        var options = Options.Create(new SerialPortOptions
        {
            Enabled = true,
            PortName = "COM_DH10_TEST",
            BarcodeChannelCapacity = 10,
            ReadTimeoutMilliseconds = 50
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppConfiguration:MachineId"] = "M-01"
            })
            .Build();

        using var scanner = new SerialPortScannerService(
            options,
            configuration,
            Substitute.For<IMediator>(),
            NullLogger<SerialPortScannerService>.Instance);

        var open = async () => await scanner.OpenAsync();
        await open.Should().NotThrowAsync();

        scanner.IsConnected.Should().BeFalse();
        scanner.State.Should().BeOneOf(DeviceConnectionState.Reconnecting, DeviceConnectionState.Connecting);

        await scanner.CloseAsync();
        scanner.State.Should().Be(DeviceConnectionState.Disconnected);
    }

    [Fact]
    public void Options_ExposeBoundedChannelAndReadTimeoutDefaults()
    {
        var options = new SerialPortOptions();
        options.BarcodeChannelCapacity.Should().Be(1000);
        options.ReadTimeoutMilliseconds.Should().Be(1000);
    }
}
