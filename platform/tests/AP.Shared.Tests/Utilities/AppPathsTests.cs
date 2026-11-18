using AP.Shared.Utilities.Helpers;
using AP.Shared.Utilities.Paths;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace AP.Shared.Tests.Utilities;

[CollectionDefinition("AppPathsStatic", DisableParallelization = true)]
public class AppPathsStaticCollection
{
}

[Collection("AppPathsStatic")]
public class AppPathsTests
{
    [Fact]
    public void ResolveDataRoot_WithExplicitConfigPath_UsesThatPath()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ap-paths-{Guid.NewGuid():N}");
        var originalEnvironment = Environment.GetEnvironmentVariable(AppPaths.DataRootEnvironmentVariable);
        var configuration = Substitute.For<IConfiguration>();
        configuration["AppRuntime:DataRoot"].Returns(tempRoot);

        try
        {
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvironmentVariable, null);

            var resolved = AppPaths.ResolveDataRoot(configuration);

            resolved.Should().Be(Path.GetFullPath(tempRoot));
            Directory.Exists(Path.Combine(resolved, "Configuration")).Should().BeTrue();
            Directory.Exists(Path.Combine(resolved, "logs")).Should().BeTrue();
            Directory.Exists(Path.Combine(resolved, "reports")).Should().BeTrue();
        }
        finally
        {
            Environment.SetEnvironmentVariable(AppPaths.DataRootEnvironmentVariable, originalEnvironment);
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolvePath_RelativePath_ResolvesUnderDataRoot()
    {
        var resolved = AppPaths.ResolvePath("reports/2026/a.xlsx");

        resolved.Should().Be(Path.Combine(AppPaths.DataRoot, "reports/2026/a.xlsx"));
    }

    [Fact]
    public void ResolvePath_AbsolutePath_IsReturnedUnchanged()
    {
        var absolute = Path.Combine(Path.GetTempPath(), "ap-absolute", "a.xlsx");

        AppPaths.ResolvePath(absolute).Should().Be(absolute);
    }

    [Fact]
    public void Initialize_ThenConfigurationHelper_WritesOverlayUnderDataRoot()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"ap-config-{Guid.NewGuid():N}");

        try
        {
            AppPaths.Initialize(tempRoot);

            ConfigurationHelper.UpdateAppSetting(
                "AppConfiguration",
                new { CompanyName = "测试公司" },
                "appsettings.Standalone.json");

            var overlay = Path.Combine(tempRoot, "Configuration", "appsettings.Standalone.json");
            File.Exists(overlay).Should().BeTrue();

            using var document = JsonDocument.Parse(File.ReadAllText(overlay));
            document.RootElement
                .GetProperty("AppConfiguration")
                .GetProperty("CompanyName")
                .GetString()
                .Should().Be("测试公司");
        }
        finally
        {
            AppPaths.ResetForTests();
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, recursive: true);
        }
    }
}
