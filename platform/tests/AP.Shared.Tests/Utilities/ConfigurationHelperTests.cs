using AP.Shared.Utilities.Helpers;
using FluentAssertions;
using Xunit;

namespace AP.Shared.Tests.Utilities;

public class ConfigurationHelperTests
{
    private class TestConfig
    {
        public string Name { get; set; } = "default";
        public int Value { get; set; } = 42;
    }

    [Fact]
    public void UpdateAppSetting_WithNullSection_Throws()
    {
        Assert.Throws<ArgumentException>(() => ConfigurationHelper.UpdateAppSetting("", new TestConfig()));
    }

    [Fact]
    public void UpdateAppSetting_WithNullValue_DoesNotThrow()
    {
        // Should not throw, just log an error internally
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting<object?>("TestSection", null!));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_WithSingleLevelPath_ProcessesSuccessfully()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("AppSettings", new TestConfig { Name = "test", Value = 100 }));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_WithNestedPath_ProcessesSuccessfully()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("Plugins:Scanner:SerialPort",
                new { PortName = "COM1", BaudRate = 9600 }));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_WithDeepNestedPath_ProcessesSuccessfully()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("A:B:C:D:E",
                new { Key = "value" }));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_WithCustomFileName_ProcessesSuccessfully()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("Test", new TestConfig(), "customsettings.json"));
        exception.Should().BeNull();
    }

    [Fact]
    public void ConfigurationHelper_IsStaticClass()
    {
        typeof(ConfigurationHelper).IsAbstract.Should().BeTrue();
        typeof(ConfigurationHelper).IsSealed.Should().BeTrue();
    }

    [Fact]
    public void UpdateAppSetting_DoesNotThrow_WhenFileDoesNotExist()
    {
        // If the Configuration directory doesn't exist, it should silently handle
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("Test", new TestConfig(), "nonexistent_file.json"));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_AcceptsPrimitiveTypes()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("Debug:Enabled", true));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_AcceptsNumericValues()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("MaxRetries", 5));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_AcceptsStringValues()
    {
        var exception = Record.Exception(() =>
            ConfigurationHelper.UpdateAppSetting("ConnectionString", "Server=localhost;Database=test"));
        exception.Should().BeNull();
    }

    [Fact]
    public void UpdateAppSetting_Throws_WhenJsonMalformed()
    {
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration");
        Directory.CreateDirectory(configDir);
        var filePath = Path.Combine(configDir, "malformed_test.json");
        File.WriteAllText(filePath, "{ not valid json");

        try
        {
            Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
                ConfigurationHelper.UpdateAppSetting("Test", new TestConfig(), "malformed_test.json"));
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [Fact]
    public void UpdateAppSetting_WritesAtomically_WithoutLeavingTempFile()
    {
        var configDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration");
        Directory.CreateDirectory(configDir);
        var filePath = Path.Combine(configDir, "atomic_test.json");
        File.WriteAllText(filePath, "{}");

        try
        {
            ConfigurationHelper.UpdateAppSetting("Atomic:Key", "value", "atomic_test.json");

            File.Exists(filePath + ".tmp").Should().BeFalse();
            File.ReadAllText(filePath).Should().Contain("\"Key\": \"value\"");
        }
        finally
        {
            File.Delete(filePath);
        }
    }
}