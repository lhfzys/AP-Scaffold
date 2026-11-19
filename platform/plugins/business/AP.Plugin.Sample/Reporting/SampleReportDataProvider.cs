using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;

namespace AP.Plugin.Sample.Reporting;

/// <summary>示例报表数据提供者：演示业务插件如何接入报表中心。</summary>
public sealed class SampleReportDataProvider : IReportDataProvider
{
    public string ReportType => "SampleBusiness";
    public string ReportName => "示例业务日报";

    public Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        var data = new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            Headers = ["序号", "项目", "数值"],
            Rows =
            [
                [1, "示例计数", 42],
                [2, "示例状态", "正常"]
            ],
            Summary = new Dictionary<string, object> { ["Total"] = 42 }
        };
        return Task.FromResult(data);
    }
}
