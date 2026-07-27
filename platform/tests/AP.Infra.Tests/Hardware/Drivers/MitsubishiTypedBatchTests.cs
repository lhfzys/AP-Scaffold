using AP.Contracts.Hardware.DeviceRuntime;
using AP.Plugin.Plc.Mitsubishi.Addressing;
using AP.Plugin.Plc.Mitsubishi.Configuration;
using AP.Plugin.Plc.Mitsubishi.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly;
using Xunit;

namespace AP.Infra.Tests.Hardware.Drivers;

/// <summary>
/// TP2-三菱：带类型批量读（循环逐条）的契约行为。
/// </summary>
public class MitsubishiTypedBatchTests
{
    [Fact]
    public async Task ReadBatchAsync_EmptyItems_ReturnsEmptyDictionary()
    {
        var service = CreateService();

        var result = await service.ReadBatchAsync(new List<BatchReadItem>());

        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData(TagDataType.Double)]
    [InlineData(TagDataType.Int64)]
    [InlineData(TagDataType.String)]
    [InlineData(TagDataType.ByteArray)]
    public async Task ReadBatchAsync_UnsupportedType_ThrowsNotSupportedException(TagDataType type)
    {
        var service = CreateService();

        var act = () => service.ReadBatchAsync(new List<BatchReadItem> { new("D0", type) });

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*暂不支持*");
    }

    [Fact]
    public async Task ReadBatchAsync_InvalidAddress_ThrowsAddressException()
    {
        var service = CreateService();

        var act = () => service.ReadBatchAsync(new List<BatchReadItem> { new("Q1", TagDataType.Int16) });

        await act.Should().ThrowAsync<MitsubishiAddressException>();
    }

    private static MitsubishiPlcService CreateService()
    {
        return new MitsubishiPlcService(
            Options.Create(new MitsubishiPlcOptions()),
            ResiliencePipeline.Empty,
            Substitute.For<ILogger<MitsubishiPlcService>>(),
            Substitute.For<IMediator>());
    }
}
