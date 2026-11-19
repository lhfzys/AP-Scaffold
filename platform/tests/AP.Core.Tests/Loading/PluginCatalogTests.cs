using AP.Core.PluginFramework.Loading;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.Loading;

public class PluginCatalogTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"plugin_catalog_{Guid.NewGuid():N}.json");

    [Fact]
    public void Load_MissingFile_DefaultsToFullAndEnabled()
    {
        var catalog = PluginCatalog.Load(_file);

        catalog.IsProfileKnown("Full").Should().BeTrue();
        catalog.IsIncluded("Full", "AP.Plugin.Anything").Should().BeTrue();
        catalog.IsEnabled("AP.Plugin.Anything").Should().BeTrue();
    }

    [Fact]
    public void Load_ProfileAndEnabledState_AreApplied()
    {
        File.WriteAllText(_file, """
            {
              "profiles": {
                "Minimal": [ "AP.Plugin.Layout", "AP.Plugin.Sample" ]
              },
              "plugins": {
                "AP.Plugin.Sample": { "enabled": false }
              }
            }
            """);

        var catalog = PluginCatalog.Load(_file);

        catalog.IsProfileKnown("Minimal").Should().BeTrue();
        catalog.IsIncluded("Minimal", "AP.Plugin.Layout").Should().BeTrue();
        catalog.IsIncluded("Minimal", "AP.Plugin.AuditLog").Should().BeFalse();
        catalog.IsEnabled("AP.Plugin.Sample").Should().BeFalse();
        catalog.IsEnabled("AP.Plugin.Layout").Should().BeTrue();
    }

    public void Dispose()
    {
        try { if (File.Exists(_file)) File.Delete(_file); } catch { }
    }
}
