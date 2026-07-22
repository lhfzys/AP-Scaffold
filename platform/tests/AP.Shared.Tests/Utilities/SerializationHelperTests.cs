using AP.Shared.Utilities.Helpers;
using FluentAssertions;
using Xunit;

namespace AP.Shared.Tests.Utilities;

public class SerializationHelperTests
{
    private class TestData
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
        public string? NullableField { get; set; }
        public TestStatus Status { get; set; }
    }

    private enum TestStatus
    {
        Pending,
        Active,
        Completed
    }

    [Fact]
    public void ToJson_SerializesObject()
    {
        var data = new TestData { Name = "Test", Value = 42 };

        var json = SerializationHelper.ToJson(data);

        json.Should().Contain("\"Name\"");
        json.Should().Contain("Test");
        json.Should().Contain("42");
    }

    [Fact]
    public void ToJson_NullValues_AreIgnored()
    {
        var data = new TestData { Name = "Test", Value = 10, NullableField = null };

        var json = SerializationHelper.ToJson(data);

        json.Should().NotContain("NullableField");
    }

    [Fact]
    public void FromJson_DeserializesObject()
    {
        const string json = """{"Name":"Test","Value":42}""";

        var result = SerializationHelper.FromJson<TestData>(json);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FromJson_WithNullString_ReturnsDefault()
    {
        var result = SerializationHelper.FromJson<TestData>(null!);

        result.Should().BeNull();
    }

    [Fact]
    public void FromJson_WithEmptyString_ReturnsDefault()
    {
        var result = SerializationHelper.FromJson<TestData>("");

        result.Should().BeNull();
    }

    [Fact]
    public void FromJson_WithWhitespace_ReturnsDefault()
    {
        var result = SerializationHelper.FromJson<TestData>("   ");

        result.Should().BeNull();
    }

    [Fact]
    public void RoundTrip_Object_ReturnsIdenticalData()
    {
        var original = new TestData
        {
            Name = "RoundTrip",
            Value = 99,
            NullableField = "not-null"
        };

        var json = SerializationHelper.ToJson(original);
        var deserialized = SerializationHelper.FromJson<TestData>(json);

        deserialized.Should().NotBeNull();
        deserialized!.Name.Should().Be(original.Name);
        deserialized.Value.Should().Be(original.Value);
        deserialized.NullableField.Should().Be(original.NullableField);
    }

    [Fact]
    public void ToJson_Enum_SerializesAsString()
    {
        var data = new TestData { Name = "EnumTest", Value = 1, Status = TestStatus.Active };

        var json = SerializationHelper.ToJson(data);

        json.Should().Contain("\"Active\"");
    }

    [Fact]
    public void FromJson_Enum_DeserializesFromString()
    {
        const string json = """{"Name":"Test","Value":1,"Status":"Completed"}""";

        var result = SerializationHelper.FromJson<TestData>(json);

        result.Should().NotBeNull();
        result!.Status.Should().Be(TestStatus.Completed);
    }

    [Fact]
    public void FromJson_CaseInsensitive_Works()
    {
        const string json = """{"name":"test","value":42}""";

        var result = SerializationHelper.FromJson<TestData>(json);

        result.Should().NotBeNull();
        result!.Name.Should().Be("test");
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FromJson_WithTypeParameter_DeserializesCorrectly()
    {
        const string json = """{"Name":"Typed","Value":7}""";

        var result = SerializationHelper.FromJson(json, typeof(TestData));

        result.Should().NotBeNull();
        result.Should().BeOfType<TestData>();
        var typed = (TestData)result!;
        typed.Name.Should().Be("Typed");
        typed.Value.Should().Be(7);
    }

    [Fact]
    public void FromJson_WithTypeParameterAndNullString_ReturnsNull()
    {
        var result = SerializationHelper.FromJson(null!, typeof(TestData));
        result.Should().BeNull();
    }

    [Fact]
    public void ToJson_DoesNotIndent()
    {
        var data = new TestData { Name = "NoIndent", Value = 1 };

        var json = SerializationHelper.ToJson(data);

        // With WriteIndented = false, the JSON should be on one line (no newlines)
        json.Should().NotContain("\n");
        json.Should().NotContain("\r");
    }
}