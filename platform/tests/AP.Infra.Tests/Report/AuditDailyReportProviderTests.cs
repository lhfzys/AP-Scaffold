using AP.Contracts.Security.Audit;
using AP.Infra.Report.Reporting;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Report;

public class AuditDailyReportProviderTests
{
    [Fact]
    public async Task GetReportData_MapsEntriesToRows()
    {
        var entries = new List<AuditLogEntry>
        {
            new() { Timestamp = new DateTime(2026, 7, 28, 10, 0, 0), UserName = "admin", ActionName = "登录", Succeeded = true },
            new() { Timestamp = new DateTime(2026, 7, 28, 9, 0, 0), UserName = "system", ActionName = "PLC 写入", TargetId = "D100", Succeeded = false, ErrorMessage = "超时" },
        };
        var provider = new AuditDailyReportProvider(CreateAudit(entries));
        var date = new DateTime(2026, 7, 28);

        var data = await provider.GetReportDataAsync(date);

        data.ReportType.Should().Be("AuditDaily");
        data.ReportDate.Should().Be(date);
        data.Rows.Should().HaveCount(2);
        // 按时间升序：9 点在前
        data.Rows[0][2].Should().Be("system");
        data.Rows[0][5].Should().Be("失败");
        data.Rows[1][2].Should().Be("admin");
    }

    [Fact]
    public async Task GetReportData_SummaryCountsSuccessAndFailure()
    {
        var entries = new List<AuditLogEntry>
        {
            new() { Timestamp = DateTime.Today, Succeeded = true },
            new() { Timestamp = DateTime.Today, Succeeded = true },
            new() { Timestamp = DateTime.Today, Succeeded = false },
        };
        var provider = new AuditDailyReportProvider(CreateAudit(entries));

        var data = await provider.GetReportDataAsync(DateTime.Today);

        data.Summary["总条数"].Should().Be(3);
        data.Summary["成功数"].Should().Be(2);
        data.Summary["失败数"].Should().Be(1);
    }

    [Fact]
    public async Task GetReportData_QueriesWholeDay()
    {
        var audit = Substitute.For<IAuditService>();
        audit.QueryAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(),
                Arg.Any<AuditActionType?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<AuditLogEntry>());
        var provider = new AuditDailyReportProvider(audit);
        var date = new DateTime(2026, 7, 28);

        await provider.GetReportDataAsync(date);

        await audit.Received(1).QueryAsync(
            new DateTime(2026, 7, 28),
            new DateTime(2026, 7, 29),
            Arg.Any<string?>(), Arg.Any<AuditActionType?>(),
            Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetReportData_EmptyDay_ReturnsEmptyRows()
    {
        var provider = new AuditDailyReportProvider(CreateAudit(new List<AuditLogEntry>()));

        var data = await provider.GetReportDataAsync(DateTime.Today);

        data.Rows.Should().BeEmpty();
        data.Summary["总条数"].Should().Be(0);
    }

    private static IAuditService CreateAudit(List<AuditLogEntry> entries)
    {
        var audit = Substitute.For<IAuditService>();
        audit.QueryAsync(Arg.Any<DateTime?>(), Arg.Any<DateTime?>(), Arg.Any<string?>(),
                Arg.Any<AuditActionType?>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(entries);
        return audit;
    }
}
