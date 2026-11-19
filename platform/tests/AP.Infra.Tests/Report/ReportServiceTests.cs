using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;
using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Configuration;
using AP.Infra.Report.Entities;
using AP.Infra.Report.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Report;

public class ReportServiceTests
{
    private readonly IReportDataProvider _provider = Substitute.For<IReportDataProvider>();
    private readonly IExcelExporter _exporter = Substitute.For<IExcelExporter>();
    private readonly IReportStorage _storage = Substitute.For<IReportStorage>();
    private readonly IReportRepository _repository = Substitute.For<IReportRepository>();

    public ReportServiceTests()
    {
        _provider.ReportType.Returns("Test");
        _provider.ReportName.Returns("测试报表");
        _provider.GetReportDataAsync(Arg.Any<DateTime>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ReportData
            {
                ReportType = "Test",
                ReportName = "测试报表",
                Headers = ["列"],
                Rows = [["值"]]
            }));
        _storage.GetFilePath(Arg.Any<DateTime>(), "Test").Returns("/tmp/test.xlsx");
        _storage.FileExists(Arg.Any<string>()).Returns(true);
        _storage.GetFileSize(Arg.Any<string>()).Returns(100);
        _exporter.ExportAsync(Arg.Any<ReportData>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task Generate_FailedArchive_RegeneratesInsteadOfReturningFailedPath()
    {
        var date = DateTime.Today;
        _repository.GetByDateAndTypeAsync(date, "Test", Arg.Any<CancellationToken>())
            .Returns(new ReportArchive
            {
                Id = "old",
                ReportDate = date,
                ReportType = "Test",
                FilePath = "/tmp/old.xlsx",
                Status = ArchiveStatus.Failed,
                FailureReason = "old failure"
            });

        var service = CreateService();

        var path = await service.GenerateReportAsync("Test", date);

        path.Should().Be("/tmp/test.xlsx");
        await _exporter.Received(1).ExportAsync(
            Arg.Any<ReportData>(), "/tmp/test.xlsx", Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).UpdateAsync(
            Arg.Is<ReportArchive>(a => a.Status == ArchiveStatus.Success && a.Id == "old"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Generate_SuccessArchiveWithExistingFile_IsSkipped()
    {
        var date = DateTime.Today;
        _repository.GetByDateAndTypeAsync(date, "Test", Arg.Any<CancellationToken>())
            .Returns(new ReportArchive
            {
                Id = "ok",
                ReportDate = date,
                ReportType = "Test",
                FilePath = "/tmp/ok.xlsx",
                Status = ArchiveStatus.Success
            });

        var service = CreateService();

        var path = await service.GenerateReportAsync("Test", date);

        path.Should().Be("/tmp/ok.xlsx");
        await _exporter.DidNotReceive().ExportAsync(
            Arg.Any<ReportData>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    private ReportService CreateService()
        => new(
            [_provider],
            _exporter,
            _storage,
            _repository,
            Options.Create(new ReportOptions()),
            NullLogger<ReportService>.Instance);
}
