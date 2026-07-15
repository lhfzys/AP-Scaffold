using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Entities;

namespace AP.Infra.Report.Reporting;

/// <summary>
/// 示例报表数据提供者
/// 用于报表中心骨架演示，后续可由业务插件替换为真实数据提供者
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
