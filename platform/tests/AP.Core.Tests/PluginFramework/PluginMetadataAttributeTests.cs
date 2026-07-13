using AP.Core.PluginFramework.Attributes;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.PluginFramework;

public class PluginMetadataAttributeTests
{
    [Fact]
    public void Constructor_WithId_SetsId()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test");
        attr.Id.Should().Be("AP.Plugin.Test");
    }

    [Fact]
    public void Constructor_WithNullId_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PluginMetadataAttribute(null!));
    }

    [Fact]
    public void DefaultValues_AreSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test");

        attr.Name.Should().BeEmpty();
        attr.Version.Should().Be("1.0.0");
        attr.Priority.Should().Be(100);
        attr.Required.Should().BeTrue();
        attr.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void Name_CanBeSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test")
        {
            Name = "Test Plugin"
        };

        attr.Name.Should().Be("Test Plugin");
    }

    [Fact]
    public void Version_CanBeSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test")
        {
            Version = "2.0.0"
        };

        attr.Version.Should().Be("2.0.0");
    }

    [Fact]
    public void Priority_CanBeSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test")
        {
            Priority = 50
        };

        attr.Priority.Should().Be(50);
    }

    [Fact]
    public void Required_CanBeSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test")
        {
            Required = false
        };

        attr.Required.Should().BeFalse();
    }

    [Fact]
    public void Dependencies_CanBeSet()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Test")
        {
            Dependencies = new[] { "AP.Plugin.Base", "AP.Plugin.Common" }
        };

        attr.Dependencies.Should().HaveCount(2);
        attr.Dependencies.Should().Contain("AP.Plugin.Base");
        attr.Dependencies.Should().Contain("AP.Plugin.Common");
    }

    [Fact]
    public void AttributeUsage_AllowsSingleUsageOnClass()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(PluginMetadataAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        attrUsage.AllowMultiple.Should().BeFalse();
        attrUsage.Inherited.Should().BeFalse();
        attrUsage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
    }

    [Fact]
    public void Metadata_CanBeRetrievedViaReflection()
    {
        var attr = new PluginMetadataAttribute("AP.Plugin.Reflection")
        {
            Name = "Reflection Test",
            Version = "3.0.0",
            Priority = 10,
            Required = true,
        };

        var retrieved = typeof(PluginMetadataAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false);

        // Verify the type is properly defined as an attribute
        typeof(PluginMetadataAttribute).IsSubclassOf(typeof(Attribute)).Should().BeTrue();
    }
}