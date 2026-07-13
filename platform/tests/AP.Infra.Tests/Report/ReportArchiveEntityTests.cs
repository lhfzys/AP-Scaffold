using AP.Infra.Report.Entities;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Report;

public class ReportArchiveEntityTests
{
    [Fact]
    public void Constructor_GeneratesId()
    {
        var archive = new ReportArchive();
        archive.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Id_IsUnique_PerInstance()
    {
        var archive1 = new ReportArchive();
        var archive2 = new ReportArchive();

        archive1.Id.Should().NotBe(archive2.Id);
    }

    [Fact]
    public void DefaultStatus_IsSuccess()
    {
        var archive = new ReportArchive();
        archive.Status.Should().Be(ArchiveStatus.Success);
    }

    [Fact]
    public void DefaultFailureReason_IsNull()
    {
        var archive = new ReportArchive();
        archive.FailureReason.Should().BeNull();
    }

    [Fact]
    public void AllProperties_CanBeSet()
    {
        var now = DateTime.UtcNow;
        var archive = new ReportArchive
        {
            Id = "test-id-123",
            ReportDate = new DateTime(2026, 7, 13),
            ReportType = "Production",
            ReportName = "Daily Production Report",
            FilePath = "reports/2026/07/13_production.xlsx",
            RecordCount = 1500,
            FileSize = 1024000,
            GeneratedAt = now,
            Status = ArchiveStatus.Success,
            FailureReason = null
        };

        archive.Id.Should().Be("test-id-123");
        archive.ReportDate.Should().Be(new DateTime(2026, 7, 13));
        archive.ReportType.Should().Be("Production");
        archive.ReportName.Should().Be("Daily Production Report");
        archive.FilePath.Should().Be("reports/2026/07/13_production.xlsx");
        archive.RecordCount.Should().Be(1500);
        archive.FileSize.Should().Be(1024000);
        archive.GeneratedAt.Should().Be(now);
        archive.Status.Should().Be(ArchiveStatus.Success);
    }

    [Fact]
    public void FailureReason_CanBeSet_WhenStatusIsFailed()
    {
        var archive = new ReportArchive
        {
            Status = ArchiveStatus.Failed,
            FailureReason = "Database connection timeout"
        };

        archive.Status.Should().Be(ArchiveStatus.Failed);
        archive.FailureReason.Should().Be("Database connection timeout");
    }

    [Fact]
    public void Status_CanBeCleaned()
    {
        var archive = new ReportArchive { Status = ArchiveStatus.Cleaned };
        archive.Status.Should().Be(ArchiveStatus.Cleaned);
    }

    [Fact]
    public void ArchiveStatus_EnumValues()
    {
        ((int)ArchiveStatus.Success).Should().Be(0);
        ((int)ArchiveStatus.Failed).Should().Be(1);
        ((int)ArchiveStatus.Cleaned).Should().Be(2);
    }

    [Fact]
    public void RecordCount_CanBeZero()
    {
        var archive = new ReportArchive { RecordCount = 0 };
        archive.RecordCount.Should().Be(0);
    }

    [Fact]
    public void FileSize_CanBeLarge()
    {
        var archive = new ReportArchive { FileSize = long.MaxValue };
        archive.FileSize.Should().Be(long.MaxValue);
    }

    [Fact]
    public void ReportArchive_IsReferenceType()
    {
        var archive = new ReportArchive();
        archive.Should().BeOfType<ReportArchive>();
    }
}