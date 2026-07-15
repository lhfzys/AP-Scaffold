namespace AP.Contracts.Report.Models;

/// <summary>
/// 报表归档记录 DTO
/// </summary>
public class ReportArchiveDto
{
    public string Id { get; set; } = string.Empty;

    public DateTime ReportDate { get; set; }

    public string ReportType { get; set; } = string.Empty;

    public string ReportName { get; set; } = string.Empty;

    public string FilePath { get; set; } = string.Empty;

    public int RecordCount { get; set; }

    public long FileSize { get; set; }

    public DateTime GeneratedAt { get; set; }

    public string Status { get; set; } = "Success";

    public string? FailureReason { get; set; }
}
