using FreeSql.DataAnnotations;

namespace AP.Infra.Report.Entities;

/// <summary>
/// 报表归档记录
/// 记录每次报表生成的元数据
/// </summary>
[Table(Name = "report_archives")]
public class ReportArchive
{
    /// <summary>
    /// 主键
    /// </summary>
    [Column(IsPrimary = true)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// 报表日期
    /// </summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// 报表类型标识
    /// </summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// 报表显示名称
    /// </summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 数据行数
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// 文件大小（字节）
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// 生成时间
    /// </summary>
    public DateTime GeneratedAt { get; set; }

    /// <summary>
    /// 归档状态
    /// </summary>
    public ArchiveStatus Status { get; set; } = ArchiveStatus.Success;

    /// <summary>
    /// 失败原因（如果状态为Failed）
    /// </summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// 归档状态
/// </summary>
public enum ArchiveStatus
{
    /// <summary>
    /// 成功
    /// </summary>
    Success = 0,

    /// <summary>
    /// 失败
    /// </summary>
    Failed = 1,

    /// <summary>
    /// 已清理
    /// </summary>
    Cleaned = 2
}