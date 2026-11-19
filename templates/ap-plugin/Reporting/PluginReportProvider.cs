using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;

namespace AP.Plugin.Sample.Reporting;

/// <summary>报表数据提供者示例；不需要报表时删除本文件及 Plugin.cs 中的注册。</summary>
public sealed class PluginReportProvider : IReportDataProvider
{
    public string ReportType => "SampleBusiness";
    public string ReportName => "示例业务日报";

    public Task<ReportData> GetReportDataAsync(DateTime date, CancellationToken ct = default)
    {
        return Task.FromResult(new ReportData
        {
            ReportType = ReportType,
            ReportName = ReportName,
            ReportDate = date,
            Headers = ["序号", "项目", "数值"],
            Rows = [[1, "示例计数", 42], [2, "示例状态", "正常"]],
            Summary = new Dictionary<string, object> { ["Total"] = 42 }
        });
    }
}
