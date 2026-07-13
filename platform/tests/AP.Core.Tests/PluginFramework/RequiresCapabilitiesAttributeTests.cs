using AP.Core.Capability;
using AP.Core.PluginFramework.Attributes;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.PluginFramework;

public class RequiresCapabilitiesAttributeTests
{
    [Fact]
    public void Constructor_WithCapabilities_SetsCapabilities()
    {
        var attr = new RequiresCapabilitiesAttribute(PluginCapabilities.Standard);
        attr.Capabilities.Should().Be(PluginCapabilities.Standard);
    }

    [Fact]
    public void Constructor_WithSingleCapability_SetsCorrectly()
    {
        var attr = new RequiresCapabilitiesAttribute(PluginCapabilities.AccessDatabase);
        attr.Capabilities.Should().Be(PluginCapabilities.AccessDatabase);
    }

    [Fact]
    public void Constructor_WithMultipleCapabilities_SetsCorrectly()
    {
        var attr = new RequiresCapabilitiesAttribute(
            PluginCapabilities.ReadConfiguration | PluginCapabilities.WriteConfiguration);
        attr.Capabilities.Should().Be(PluginCapabilities.ReadConfiguration | PluginCapabilities.WriteConfiguration);
    }

    [Fact]
    public void Constructor_WithFullAccess_SetsFullAccess()
    {
        var attr = new RequiresCapabilitiesAttribute(PluginCapabilities.FullAccess);
        attr.Capabilities.Should().Be(PluginCapabilities.FullAccess);
    }

    [Fact]
    public void Constructor_WithNone_SetsNone()
    {
        var attr = new RequiresCapabilitiesAttribute(PluginCapabilities.None);
        attr.Capabilities.Should().Be(PluginCapabilities.None);
    }

    [Fact]
    public void AttributeUsage_CanBeAppliedToClass()
    {
        var attrUsage = (AttributeUsageAttribute)typeof(RequiresCapabilitiesAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)[0];

        attrUsage.AllowMultiple.Should().BeFalse();
        attrUsage.Inherited.Should().BeTrue();
        attrUsage.ValidOn.Should().HaveFlag(AttributeTargets.Class);
    }

    [Fact]
    public void Attribute_CanBeRetrievedViaReflection()
    {
        // Simulate applying the attribute and retrieving it
        var attr = new RequiresCapabilitiesAttribute(PluginCapabilities.Hardware);

        var capabilities = attr.Capabilities;
        capabilities.Should().HaveFlag(PluginCapabilities.AccessPLC);
        capabilities.Should().HaveFlag(PluginCapabilities.AccessSerialPort);
        capabilities.Should().HaveFlag(PluginCapabilities.AccessNetwork);
        capabilities.Should().HaveFlag(PluginCapabilities.Standard);
    }

    [Fact]
    public void Attribute_IsAssignableFromAttribute()
    {
        typeof(RequiresCapabilitiesAttribute).IsSubclassOf(typeof(Attribute)).Should().BeTrue();
    }
}