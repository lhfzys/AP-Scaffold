using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;

namespace AP.Infra.Report.Reporting;

/// <summary>
/// 示例报表数据提供者
/// 仅作开发参考（展示 IReportDataProvider 的实现方式），不在 DI 注册；
/// 生产环境使用 AuditDailyReportProvider（审计日报）及后续业务提供者。
/// </summary>
public class SampleReportDataProvider : IReportDataProvider
{
    public string ReportType => "Sample";

    public string ReportName => "示例日报";

    public Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        var data = new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            Headers = ["序号", "项目", "数值", "单位"],
            Rows =
            [
                [1, "产量", 1200, "件"],
                [2, "良品数", 1180, "件"],
                [3, "不良品数", 20, "件"],
                [4, "合格率", "98.33%", "-"]
            ],
            Summary = new Dictionary<string, object>
            {
                ["Total"] = 1200,
                ["PassRate"] = "98.33%"
            }
        };

        return Task.FromResult(data);
    }
}
