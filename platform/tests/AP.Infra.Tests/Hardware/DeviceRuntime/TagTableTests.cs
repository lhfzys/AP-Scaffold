using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TagTableTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void MissingFile_ReturnsEmptyTable()
    {
        var table = CreateTable([], Path.Combine(Path.GetTempPath(), $"nonexistent-{Guid.NewGuid()}.json"));

        table.Tags.Should().BeEmpty();
    }

    [Fact]
    public void ValidFile_LoadsResolvesAndCachesParsedAddress()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw100", "DataType": "Int16" } ] }
            """);

        var table = CreateTable([new FakeValidator("Test")], path);

        var tag = table.Find("A.B");
        tag.Should().NotBeNull();
        tag!.NormalizedAddress.Should().Be("NORMALIZED:raw100");
        tag.ParsedAddress.Should().BeOfType<FakeParsedAddress>();
    }

    [Fact]
    public void Find_CaseInsensitive()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);
        var table = CreateTable([new FakeValidator("Test")], path);

        table.Find("a.b").Should().NotBeNull();
    }

    [Fact]
    public void DuplicateName_ThrowsWithAggregatedError()
    {
        var path = WriteTags("""
            { "Tags": [
                { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" },
                { "Name": "a.b", "DeviceId": "plc.main", "Address": "raw2" }
            ] }
            """);

        var act = () => CreateTable([new FakeValidator("Test")], path);

        act.Should().Throw<DeviceConfigurationException>().WithMessage("*点名重复*");
    }

    [Fact]
    public void UnknownDevice_Throws()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "ghost", "Address": "raw1" } ] }
            """);

        var act = () => CreateTable([new FakeValidator("Test")], path);

        act.Should().Throw<DeviceConfigurationException>().WithMessage("*ghost*");
    }

    [Fact]
    public void NoValidatorForDriver_Throws()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);

        var act = () => CreateTable([], path); // 无验证器

        act.Should().Throw<DeviceConfigurationException>().WithMessage("*无地址验证器*");
    }

    [Fact]
    public void InvalidAddress_ThrowsWithReason()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "BAD!" } ] }
            """);

        var act = () => CreateTable([new FakeValidator("Test")], path);

        act.Should().Throw<DeviceConfigurationException>().WithMessage("*地址非法*");
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }

    [Fact]
    public void AcquisitionSection_IsParsed_WithOverrideAndDefault()
    {
        var path = WriteTags("""
            {
              "Acquisition": { "DefaultIntervalMs": 800, "Overrides": { "A.B": 200 } },
              "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ]
            }
            """);

        var table = CreateTable([new FakeValidator("Test")], path);

        table.Acquisition.DefaultIntervalMs.Should().Be(800);
        table.Acquisition.GetIntervalMs("A.B").Should().Be(200);
        table.Acquisition.GetIntervalMs("C.D").Should().Be(800);
    }

    [Fact]
    public void MissingAcquisitionSection_UsesDefaults()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);

        var table = CreateTable([new FakeValidator("Test")], path);

        table.Acquisition.DefaultIntervalMs.Should().Be(1000);
    }

    // --- 热重载 ---

    [Fact]
    public void Reload_PicksUpNewContent_Atomically()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);
        var table = CreateTable([new FakeValidator("Test")], path);

        File.WriteAllText(path, """
            {
              "Acquisition": { "DefaultIntervalMs": 250 },
              "Tags": [ { "Name": "C.D", "DeviceId": "plc.main", "Address": "raw2" } ]
            }
            """);

        var errors = table.Reload();

        errors.Should().BeEmpty();
        table.Find("A.B").Should().BeNull();      // 旧点已移除
        table.Find("C.D").Should().NotBeNull();   // 新点已生效
        table.Acquisition.DefaultIntervalMs.Should().Be(250); // 采集配置同步更新
    }

    [Fact]
    public void Reload_InvalidContent_KeepsOldTable()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);
        var table = CreateTable([new FakeValidator("Test")], path);

        File.WriteAllText(path, """
            { "Tags": [ { "Name": "C.D", "DeviceId": "plc.main", "Address": "BAD!" } ] }
            """);

        var errors = table.Reload();

        errors.Should().NotBeEmpty();
        table.Find("A.B").Should().NotBeNull(); // 旧表保留
        table.Find("C.D").Should().BeNull();
    }

    [Fact]
    public void Reload_MalformedJson_KeepsOldTable()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "A.B", "DeviceId": "plc.main", "Address": "raw1" } ] }
            """);
        var table = CreateTable([new FakeValidator("Test")], path);

        File.WriteAllText(path, "{ not json");

        var errors = table.Reload();

        errors.Should().NotBeEmpty();
        table.Find("A.B").Should().NotBeNull();
    }

    private string WriteTags(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    private static TagTable CreateTable(IEnumerable<IAddressValidator> validators, string path)
    {
        var registry = new DeviceRegistry();
        registry.Register(FakeDevice("plc.main", "Test"));
        return new TagTable(registry, validators, path);
    }

    private static IDevice FakeDevice(string deviceId, string driverType)
    {
        var device = Substitute.For<IDevice>();
        device.Info.Returns(new DeviceInfo(deviceId, deviceId, DeviceType.Plc, driverType));
        return device;
    }

    /// <summary>假验证器：仅拒绝含 '!' 的地址，其余包装为 FakeParsedAddress。</summary>
    private sealed class FakeValidator(string driverType) : IAddressValidator
    {
        public string DriverType => driverType;

        public bool TryParse(string address, out object? parsedAddress, out string? error)
        {
            if (address.Contains('!'))
            {
                parsedAddress = null;
                error = "含非法字符 '!'";
                return false;
            }

            parsedAddress = new FakeParsedAddress(address);
            error = null;
            return true;
        }
    }

    private sealed record FakeParsedAddress(string Raw)
    {
        public override string ToString() => $"NORMALIZED:{Raw}";
    }
}
