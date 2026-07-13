using AP.Infra.Report.Configuration;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Report;

public class ReportOptionsTests
{
    [Fact]
    public void SectionName_IsReport()
    {
        ReportOptions.SectionName.Should().Be("Report");
    }

    [Fact]
    public void DefaultValues_AreSet()
    {
        var options = new ReportOptions();

        options.Storage.Should().NotBeNull();
        options.Archive.Should().NotBeNull();
        options.Retention.Should().NotBeNull();
        options.Cleanup.Should().NotBeNull();
    }

    [Fact]
    public void StorageOptions_DefaultRootPath_IsReports()
    {
        var storage = new StorageOptions();
        storage.RootPath.Should().Be("reports");
    }

    [Fact]
    public void StorageOptions_DefaultPathFormat_ContainsVariables()
    {
        var storage = new StorageOptions();
        storage.PathFormat.Should().Be("{year}/{month}/{date}_{type}.xlsx");
    }

    [Fact]
    public void StorageOptions_DefaultTemplatePath_IsNull()
    {
        var storage = new StorageOptions();
        storage.DefaultTemplatePath.Should().BeNull();
    }

    [Fact]
    public void ArchiveOptions_DefaultEnabled_IsTrue()
    {
        var archive = new ArchiveOptions();
        archive.Enabled.Should().BeTrue();
    }

    [Fact]
    public void ArchiveOptions_DefaultTime_Is0200()
    {
        var archive = new ArchiveOptions();
        archive.Time.Should().Be("02:00");
    }

    [Fact]
    public void ArchiveOptions_DefaultReportTypes_IsEmpty()
    {
        var archive = new ArchiveOptions();
        archive.ReportTypes.Should().BeEmpty();
    }

    [Fact]
    public void RetentionOptions_DefaultEnabled_IsTrue()
    {
        var retention = new RetentionOptions();
        retention.Enabled.Should().BeTrue();
    }

    [Fact]
    public void RetentionOptions_DefaultDays_Is180()
    {
        var retention = new RetentionOptions();
        retention.Days.Should().Be(180);
    }

    [Fact]
    public void RetentionOptions_DefaultCheckInterval_IsOneDay()
    {
        var retention = new RetentionOptions();
        retention.CheckInterval.Should().Be(TimeSpan.FromDays(1));
    }

    [Fact]
    public void RetentionOptions_DefaultDeleteFiles_IsTrue()
    {
        var retention = new RetentionOptions();
        retention.DeleteFiles.Should().BeTrue();
    }

    [Fact]
    public void RetentionOptions_DefaultProtectedTypes_IsEmpty()
    {
        var retention = new RetentionOptions();
        retention.ProtectedTypes.Should().BeEmpty();
    }

    [Fact]
    public void CleanupOptions_DefaultEnabled_IsTrue()
    {
        var cleanup = new CleanupOptions();
        cleanup.Enabled.Should().BeTrue();
    }

    [Fact]
    public void CleanupOptions_DefaultDryRun_IsFalse()
    {
        var cleanup = new CleanupOptions();
        cleanup.DryRun.Should().BeFalse();
    }

    [Fact]
    public void StorageOptions_CanBeCustomized()
    {
        var storage = new StorageOptions
        {
            RootPath = "custom_reports",
            PathFormat = "custom/{type}/{date}.xlsx",
            DefaultTemplatePath = "templates/default.xlsx"
        };

        storage.RootPath.Should().Be("custom_reports");
        storage.PathFormat.Should().Be("custom/{type}/{date}.xlsx");
        storage.DefaultTemplatePath.Should().Be("templates/default.xlsx");
    }

    [Fact]
    public void ArchiveOptions_CanBeCustomized()
    {
        var archive = new ArchiveOptions
        {
            Enabled = false,
            Time = "23:30",
            ReportTypes = new List<string> { "Production", "Quality" }
        };

        archive.Enabled.Should().BeFalse();
        archive.Time.Should().Be("23:30");
        archive.ReportTypes.Should().HaveCount(2);
    }

    [Fact]
    public void RetentionOptions_CanBeCustomized()
    {
        var retention = new RetentionOptions
        {
            Enabled = false,
            Days = 90,
            CheckInterval = TimeSpan.FromHours(12),
            DeleteFiles = false,
            ProtectedTypes = new List<string> { "Monthly" }
        };

        retention.Enabled.Should().BeFalse();
        retention.Days.Should().Be(90);
        retention.CheckInterval.Should().Be(TimeSpan.FromHours(12));
        retention.DeleteFiles.Should().BeFalse();
        retention.ProtectedTypes.Should().ContainSingle();
    }

    [Fact]
    public void CleanupOptions_CanBeCustomized()
    {
        var cleanup = new CleanupOptions
        {
            Enabled = false,
            DryRun = true
        };

        cleanup.Enabled.Should().BeFalse();
        cleanup.DryRun.Should().BeTrue();
    }
}