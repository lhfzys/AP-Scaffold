using AP.Contracts.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

/// <summary>
/// T4.1 Tag 模型契约的基本行为。
/// </summary>
public class TagContractTests
{
    [Fact]
    public void TagValueGood_HasExpectedShape()
    {
        var value = TagValue.Good((short)42, version: 7);

        value.Value.Should().Be((short)42);
        value.Quality.Should().Be(TagQuality.Good);
        value.Version.Should().Be(7);
        value.Error.Should().BeNull();
        value.Timestamp.Should().BeCloseTo(DateTimeOffset.Now, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void TagValueBad_CarriesErrorWithoutValue()
    {
        var value = TagValue.Bad("设备未连接");

        value.Value.Should().BeNull();
        value.Quality.Should().Be(TagQuality.Bad);
        value.Error.Should().Be("设备未连接");
        value.Version.Should().Be(0);
    }

    [Fact]
    public void TagDefinition_Defaults_AreSensible()
    {
        var tag = new TagDefinition { Name = "Line1.Oven.Temperature", DeviceId = "plc.main", Address = "D100" };

        tag.DataType.Should().Be(TagDataType.Int16);
        tag.Access.Should().Be(TagAccess.ReadWrite);
        tag.Description.Should().BeNull();
        tag.Group.Should().BeNull();
        tag.Unit.Should().BeNull();
    }
}
