using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Attributes;
using AP.Core.PluginFramework.Loading;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.PluginFramework;

public class PluginGraphValidatorTests
{
    [Fact]
    public void Validate_EmptyList_ReturnsEmpty()
    {
        var result = PluginGraphValidator.Validate(new List<PluginDescriptor>(), out var issues);

        result.Should().BeEmpty();
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_ValidGraph_KeepsAllWithoutIssues()
    {
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.A"),
            CreateDescriptor("AP.Plugin.B", dependencies: new[] { "AP.Plugin.A" }),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().HaveCount(2);
        issues.Should().BeEmpty();
    }

    [Fact]
    public void Validate_DuplicateIds_RemovesAllCopiesWithFatalIssue()
    {
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.Dup"),
            CreateDescriptor("AP.Plugin.Dup"),
            CreateDescriptor("AP.Plugin.Other"),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().ContainSingle().Which.Metadata.Id.Should().Be("AP.Plugin.Other");
        issues.Should().ContainSingle(i => i.PluginId == "AP.Plugin.Dup" && i.IsFatal);
    }

    [Fact]
    public void Validate_MissingDependency_OptionalPlugin_RemovedWithNonFatalIssue()
    {
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.A", required: false, dependencies: new[] { "AP.Plugin.Ghost" }),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().BeEmpty();
        issues.Should().ContainSingle(i => i.PluginId == "AP.Plugin.A" && !i.IsFatal);
    }

    [Fact]
    public void Validate_MissingDependency_RequiredPlugin_RemovedWithFatalIssue()
    {
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.A", required: true, dependencies: new[] { "AP.Plugin.Ghost" }),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().BeEmpty();
        issues.Should().ContainSingle(i => i.PluginId == "AP.Plugin.A" && i.IsFatal);
    }

    [Fact]
    public void Validate_CascadingMissingDependency_RemovesDependentsRecursively()
    {
        // B 依赖缺失的 Ghost 被剔除后，依赖 B 的 A 也应被级联剔除
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.A", dependencies: new[] { "AP.Plugin.B" }),
            CreateDescriptor("AP.Plugin.B", dependencies: new[] { "AP.Plugin.Ghost" }),
            CreateDescriptor("AP.Plugin.C"),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().ContainSingle().Which.Metadata.Id.Should().Be("AP.Plugin.C");
        issues.Should().HaveCount(2);
        issues.Select(i => i.PluginId).Should().BeEquivalentTo("AP.Plugin.A", "AP.Plugin.B");
    }

    [Fact]
    public void Validate_DependencyOnDuplicateId_TreatedAsMissing()
    {
        // 重复 ID 被全部剔除后，依赖它的插件视为依赖缺失
        var descriptors = new[]
        {
            CreateDescriptor("AP.Plugin.Dup"),
            CreateDescriptor("AP.Plugin.Dup"),
            CreateDescriptor("AP.Plugin.A", dependencies: new[] { "AP.Plugin.Dup" }),
        };

        var result = PluginGraphValidator.Validate(descriptors, out var issues);

        result.Should().BeEmpty();
        issues.Should().HaveCount(2);
    }

    private static PluginDescriptor CreateDescriptor(
        string id,
        bool required = true,
        string[]? dependencies = null)
    {
        var metadata = new PluginMetadataAttribute(id)
        {
            Name = $"Test Plugin {id}",
            Version = "1.0.0",
            Required = required,
            Dependencies = dependencies ?? Array.Empty<string>(),
        };
        return new PluginDescriptor(
            metadata,
            typeof(IPlugin),
            null!, // PluginLoadContext - null for testing
            typeof(IPlugin).Assembly);
    }
}
