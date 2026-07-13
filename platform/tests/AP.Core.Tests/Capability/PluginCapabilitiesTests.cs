using AP.Core.Capability;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.Capability;

public class PluginCapabilitiesTests
{
    [Fact]
    public void None_ValueIsZero()
    {
        ((int)PluginCapabilities.None).Should().Be(0);
    }

    [Fact]
    public void ReadConfiguration_HasCorrectFlag()
    {
        ((int)PluginCapabilities.ReadConfiguration).Should().Be(1 << 0);
    }

    [Fact]
    public void WriteConfiguration_HasCorrectFlag()
    {
        ((int)PluginCapabilities.WriteConfiguration).Should().Be(1 << 1);
    }

    [Fact]
    public void AccessDatabase_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessDatabase).Should().Be(1 << 2);
    }

    [Fact]
    public void AccessFileSystem_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessFileSystem).Should().Be(1 << 3);
    }

    [Fact]
    public void AccessNetwork_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessNetwork).Should().Be(1 << 4);
    }

    [Fact]
    public void AccessPLC_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessPLC).Should().Be(1 << 5);
    }

    [Fact]
    public void AccessSerialPort_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessSerialPort).Should().Be(1 << 6);
    }

    [Fact]
    public void AccessCamera_HasCorrectFlag()
    {
        ((int)PluginCapabilities.AccessCamera).Should().Be(1 << 7);
    }

    [Fact]
    public void RegisterViews_HasCorrectFlag()
    {
        ((int)PluginCapabilities.RegisterViews).Should().Be(1 << 8);
    }

    [Fact]
    public void ShowDialogs_HasCorrectFlag()
    {
        ((int)PluginCapabilities.ShowDialogs).Should().Be(1 << 9);
    }

    [Fact]
    public void PublishEvents_HasCorrectFlag()
    {
        ((int)PluginCapabilities.PublishEvents).Should().Be(1 << 10);
    }

    [Fact]
    public void SubscribeEvents_HasCorrectFlag()
    {
        ((int)PluginCapabilities.SubscribeEvents).Should().Be(1 << 11);
    }

    [Fact]
    public void CallGrpcServices_HasCorrectFlag()
    {
        ((int)PluginCapabilities.CallGrpcServices).Should().Be(1 << 12);
    }

    [Fact]
    public void ProvideGrpcServices_HasCorrectFlag()
    {
        ((int)PluginCapabilities.ProvideGrpcServices).Should().Be(1 << 13);
    }

    [Fact]
    public void ReadOnly_IsCombinationOfReadConfigurationAndAccessDatabase()
    {
        PluginCapabilities.ReadOnly.Should().Be(
            PluginCapabilities.ReadConfiguration | PluginCapabilities.AccessDatabase);
    }

    [Fact]
    public void Standard_IsCombinationOfMultipleCapabilities()
    {
        PluginCapabilities.Standard.Should().Be(
            PluginCapabilities.ReadConfiguration |
            PluginCapabilities.AccessDatabase |
            PluginCapabilities.PublishEvents |
            PluginCapabilities.SubscribeEvents |
            PluginCapabilities.RegisterViews);
    }

    [Fact]
    public void Hardware_IncludesStandardAndHardwareCapabilities()
    {
        PluginCapabilities.Hardware.Should().HaveFlag(PluginCapabilities.Standard);
        PluginCapabilities.Hardware.Should().HaveFlag(PluginCapabilities.AccessPLC);
        PluginCapabilities.Hardware.Should().HaveFlag(PluginCapabilities.AccessSerialPort);
        PluginCapabilities.Hardware.Should().HaveFlag(PluginCapabilities.AccessNetwork);
    }

    [Fact]
    public void FullAccess_HasAllFlags()
    {
        // FullAccess should have all defined capabilities
        var allIndividualFlags = Enum.GetValues<PluginCapabilities>()
            .Where(c => c != PluginCapabilities.None &&
                        c != PluginCapabilities.ReadOnly &&
                        c != PluginCapabilities.Standard &&
                        c != PluginCapabilities.Hardware &&
                        c != PluginCapabilities.FullAccess)
            .Aggregate(PluginCapabilities.None, (acc, c) => acc | c);

        PluginCapabilities.FullAccess.Should().HaveFlag(allIndividualFlags);
    }

    [Fact]
    public void Capability_CanCombineWithBitwiseOr()
    {
        var combined = PluginCapabilities.ReadConfiguration | PluginCapabilities.AccessDatabase;
        combined.Should().HaveFlag(PluginCapabilities.ReadConfiguration);
        combined.Should().HaveFlag(PluginCapabilities.AccessDatabase);
        combined.Should().NotHaveFlag(PluginCapabilities.AccessNetwork);
    }

    [Fact]
    public void Capability_CanTestWithHasFlag()
    {
        var capabilities = PluginCapabilities.Standard;

        capabilities.HasFlag(PluginCapabilities.ReadConfiguration).Should().BeTrue();
        capabilities.HasFlag(PluginCapabilities.AccessDatabase).Should().BeTrue();
        capabilities.HasFlag(PluginCapabilities.PublishEvents).Should().BeTrue();
        capabilities.HasFlag(PluginCapabilities.SubscribeEvents).Should().BeTrue();
        capabilities.HasFlag(PluginCapabilities.RegisterViews).Should().BeTrue();
        capabilities.HasFlag(PluginCapabilities.AccessNetwork).Should().BeFalse();
        capabilities.HasFlag(PluginCapabilities.AccessPLC).Should().BeFalse();
    }

    [Fact]
    public void AllFlags_AreMutuallyExclusive()
    {
        var individualFlags = new[]
        {
            PluginCapabilities.ReadConfiguration,
            PluginCapabilities.WriteConfiguration,
            PluginCapabilities.AccessDatabase,
            PluginCapabilities.AccessFileSystem,
            PluginCapabilities.AccessNetwork,
            PluginCapabilities.AccessPLC,
            PluginCapabilities.AccessSerialPort,
            PluginCapabilities.AccessCamera,
            PluginCapabilities.RegisterViews,
            PluginCapabilities.ShowDialogs,
            PluginCapabilities.PublishEvents,
            PluginCapabilities.SubscribeEvents,
            PluginCapabilities.CallGrpcServices,
            PluginCapabilities.ProvideGrpcServices,
        };

        // Verify each individual flag has exactly one bit set
        foreach (var flag in individualFlags)
        {
            var value = (int)flag;
            // Check that only one bit is set (power of 2)
            (value & (value - 1)).Should().Be(0,
                $"Flag {flag} should have exactly one bit set");
        }
    }
}