using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TagTableValidatorTests
{
    [Fact]
    public void ValidTags_ReturnsNoErrors()
    {
        var validator = CreateValidator([new FakeValidator("Test")]);

        var errors = validator.Validate([Tag("A.B", "plc.main", "raw100")]);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void DuplicateName_CaseInsensitive_Reported()
    {
        var validator = CreateValidator([new FakeValidator("Test")]);

        var errors = validator.Validate([Tag("A.B", "plc.main", "raw1"), Tag("a.b", "plc.main", "raw2")]);

        errors.Should().ContainSingle().Which.Should().Contain("点名重复");
    }

    [Fact]
    public void UnknownDevice_Reported()
    {
        var validator = CreateValidator([new FakeValidator("Test")]);

        var errors = validator.Validate([Tag("A.B", "ghost", "raw1")]);

        errors.Should().ContainSingle().Which.Should().Contain("ghost");
    }

    [Fact]
    public void NoValidatorForDriver_Reported()
    {
        var validator = CreateValidator([]); // 无验证器

        var errors = validator.Validate([Tag("A.B", "plc.main", "raw1")]);

        errors.Should().ContainSingle().Which.Should().Contain("无地址验证器");
    }

    [Fact]
    public void InvalidAddress_ReportedWithReason()
    {
        var validator = CreateValidator([new FakeValidator("Test")]);

        var errors = validator.Validate([Tag("A.B", "plc.main", "BAD!")]);

        errors.Should().ContainSingle().Which.Should().Contain("地址非法");
    }

    [Fact]
    public void MultipleErrors_AreAggregated()
    {
        var validator = CreateValidator([new FakeValidator("Test")]);

        var errors = validator.Validate([
            Tag("A.B", "ghost", "raw1"),
            Tag("C.D", "plc.main", "BAD!"),
        ]);

        errors.Should().HaveCount(2);
    }

    private static TagDefinition Tag(string name, string deviceId, string address) => new()
    {
        Name = name,
        DeviceId = deviceId,
        Address = address,
    };

    private static TagTableValidator CreateValidator(IEnumerable<IAddressValidator> validators)
    {
        var registry = new DeviceRegistry();
        var device = Substitute.For<IDevice>();
        device.Info.Returns(new DeviceInfo("plc.main", "plc.main", DeviceType.Plc, "Test"));
        registry.Register(device);
        return new TagTableValidator(registry, validators);
    }

    /// <summary>假验证器：仅拒绝含 '!' 的地址，其余视为合法。</summary>
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

            parsedAddress = new object();
            error = null;
            return true;
        }
    }
}
