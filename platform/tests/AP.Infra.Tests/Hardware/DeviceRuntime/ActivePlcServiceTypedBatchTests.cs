using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

/// <summary>
/// TP1：ActivePlcService 带类型批量读取转发。
/// </summary>
public class ActivePlcServiceTypedBatchTests
{
    [Fact]
    public async Task ReadBatchAsync_DriverSupportsTypedBatch_Forwards()
    {
        var expected = new Dictionary<string, object> { ["D0"] = (short)1, ["D1"] = true };
        var driver = Substitute.For<IPlcService, IPlcTypedBatchRead>();
        ((IPlcTypedBatchRead)driver).ReadBatchAsync(Arg.Any<IReadOnlyList<BatchReadItem>>(), Arg.Any<CancellationToken>())
            .Returns(expected);

        var active = CreateActive(driver);
        var items = new List<BatchReadItem> { new("D0", TagDataType.Int16), new("D1", TagDataType.Bool) };

        var result = await active.ReadBatchAsync(items);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task ReadBatchAsync_DriverNotSupported_ThrowsNotSupportedException()
    {
        var active = CreateActive(Substitute.For<IPlcService>());

        var act = () => active.ReadBatchAsync([new BatchReadItem("D0", TagDataType.Int16)]);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*批量读取*");
    }

    private static ActivePlcService CreateActive(IPlcService driver)
    {
        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Test");
        factory.SupportedFeatures.Returns(PlcServiceFeatures.BasicReadWrite);
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(driver);

        var registry = new PlcDriverRegistry();
        registry.Register(factory);

        return new ActivePlcService(
            Options.Create(new PlcOptions { DriverType = "Test" }),
            registry,
            Substitute.For<IServiceProvider>(),
            Substitute.For<ILogger<ActivePlcService>>());
    }
}
