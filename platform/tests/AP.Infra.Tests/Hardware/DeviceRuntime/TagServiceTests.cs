using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TagServiceTests
{
    [Fact]
    public async Task Read_UnknownTag_ThrowsArgumentException()
    {
        var (service, _) = Create();

        var act = () => service.ReadAsync("Ghost.Tag");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*Ghost.Tag*");
    }

    [Fact]
    public async Task Read_WriteOnlyTag_ThrowsInvalidOperationException()
    {
        var (service, _) = Create([Tag("T1", "plc.main", "D0", TagDataType.Int16, TagAccess.WriteOnly)]);

        var act = () => service.ReadAsync("T1");

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*只写*");
    }

    [Fact]
    public async Task Read_DeviceNotConnected_ReturnsBad()
    {
        var (service, _) = Create(
            [Tag("T1", "plc.main", "D0", TagDataType.Int16)],
            deviceState: DeviceConnectionState.Reconnecting);

        var value = await service.ReadAsync("T1");

        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Contain("未连接");
    }

    [Fact]
    public async Task Read_NonPlcDevice_ReturnsBad()
    {
        var (service, _) = Create(
            [Tag("T1", "scanner.com3", "X", TagDataType.Int16)],
            deviceType: DeviceType.Scanner);

        var value = await service.ReadAsync("T1");

        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Contain("不支持");
    }

    [Fact]
    public async Task Read_Success_ReturnsGoodValue()
    {
        var (service, plc) = Create([Tag("T1", "plc.main", "D100", TagDataType.Int16)]);
        plc.ReadAsync<short>("D100", Arg.Any<CancellationToken>()).Returns((short)42);

        var value = await service.ReadAsync("T1");

        value.Quality.Should().Be(TagQuality.Good);
        value.Value.Should().Be((short)42);
        value.Version.Should().Be(0); // 直连读取不经过最新值表
    }

    [Fact]
    public async Task Read_DriverThrows_ReturnsBad()
    {
        var (service, plc) = Create([Tag("T1", "plc.main", "D100", TagDataType.Int16)]);
        plc.ReadAsync<short>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<short>>(_ => throw new Exception("读取超时"));

        var value = await service.ReadAsync("T1");

        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Be("读取超时");
    }

    [Fact]
    public async Task Read_UnsupportedType_ReturnsBad()
    {
        var (service, _) = Create([Tag("T1", "plc.main", "D100", TagDataType.Double)]);

        var value = await service.ReadAsync("T1");

        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Contain("不支持");
    }

    [Fact]
    public async Task Write_ReadOnlyTag_ThrowsInvalidOperationException()
    {
        var (service, _) = Create([Tag("T1", "plc.main", "D0", TagDataType.Int16, TagAccess.ReadOnly)]);

        var act = () => service.WriteAsync("T1", (short)1);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*只读*");
    }

    [Fact]
    public async Task Write_TypeMismatch_ThrowsArgumentException()
    {
        var (service, _) = Create([Tag("T1", "plc.main", "D0", TagDataType.Int16)]);

        var act = () => service.WriteAsync("T1", "not-a-short");

        await act.Should().ThrowAsync<ArgumentException>().WithMessage("*不匹配*");
    }

    [Fact]
    public async Task Write_Success_ReturnsGood_AndAuditedPathUsed()
    {
        var (service, plc) = Create([Tag("T1", "plc.main", "D100", TagDataType.Int16)]);

        var value = await service.WriteAsync("T1", (short)7);

        value.Quality.Should().Be(TagQuality.Good);
        // 写入经统一 IPlcService（含审计装饰器链），地址为规范化形
        await plc.Received(1).WriteAsync("D100", (short)7, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Write_DriverThrows_ReturnsBad()
    {
        var (service, plc) = Create([Tag("T1", "plc.main", "D100", TagDataType.Int16)]);
        plc.WriteAsync(Arg.Any<string>(), Arg.Any<short>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new Exception("写入被拒绝"));

        var value = await service.WriteAsync("T1", (short)7);

        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Be("写入被拒绝");
    }

    // --- 测试基础设施 ---

    private static (TagService Service, IPlcService Plc) Create(
        ResolvedTag[]? tags = null,
        DeviceConnectionState deviceState = DeviceConnectionState.Connected,
        DeviceType deviceType = DeviceType.Plc)
    {
        var device = Substitute.For<IDevice>();
        device.Info.Returns(new DeviceInfo("plc.main", "主 PLC", deviceType, "Test"));
        device.State.Returns(deviceState);

        var registry = Substitute.For<IDeviceRegistry>();
        registry.Find(Arg.Any<string>()).Returns(device);

        var plc = Substitute.For<IPlcService>();
        var table = new FakeTagTable(tags ?? [Tag("T1", "plc.main", "D0", TagDataType.Int16)]);

        return (new TagService(table, registry, plc, Substitute.For<ILogger<TagService>>()), plc);
    }

    private static ResolvedTag Tag(string name, string deviceId, string address, TagDataType type, TagAccess access = TagAccess.ReadWrite)
    {
        return new ResolvedTag(
            new TagDefinition { Name = name, DeviceId = deviceId, Address = address, DataType = type, Access = access },
            new FakeParsedAddress(address));
    }

    private sealed record FakeParsedAddress(string Address)
    {
        public override string ToString() => Address;
    }

    private sealed class FakeTagTable(ResolvedTag[] tags) : ITagTable
    {
        public IReadOnlyCollection<ResolvedTag> Tags => tags;

        public TagAcquisitionConfig Acquisition { get; } = new();

        public ResolvedTag? Find(string name) =>
            tags.FirstOrDefault(t => string.Equals(t.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
    }
}
