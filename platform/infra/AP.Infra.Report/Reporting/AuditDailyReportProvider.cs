using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;
using AP.Contracts.Security.Audit;

namespace AP.Infra.Report.Reporting;

/// <summary>
/// 操作审计日报数据提供者（框架内第一个真实数据源报表）。
/// 数据来自审计日志（PLC 写操作 ManualControl、配置修改、登录等）；
/// 审计关闭（NullAuditService 返回空）时产生空表，不报错。
/// </summary>
public class AuditDailyReportProvider : IReportDataProvider
{
    private readonly IAuditService _auditService;

    public AuditDailyReportProvider(IAuditService auditService)
    {
        _auditService = auditService;
    }

    public string ReportType => "AuditDaily";

    public string ReportName => "操作审计日报";

    public async Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        var start = date.Date;
        var end = start.AddDays(1);
        var entries = await _auditService.QueryAsync(start, end, take: 10000, ct: ct);

        var rows = entries
            .OrderBy(e => e.Timestamp)
            .Select((e, i) => new List<object>
            {
                i + 1,
                e.Timestamp.ToString("HH:mm:ss"),
                e.UserName,
                e.ActionName,
                e.TargetId ?? "-",
                e.Succeeded ? "成功" : "失败",
                e.Description ?? e.ErrorMessage ?? "-"
            })
            .ToList();

        return new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            Headers = ["序号", "时间", "操作人", "操作", "对象", "结果", "详情"],
            Rows = rows,
            Summary = new Dictionary<string, object>
            {
                ["总条数"] = entries.Count,
                ["成功数"] = entries.Count(e => e.Succeeded),
                ["失败数"] = entries.Count(e => !e.Succeeded)
            }
        };
    }
}
