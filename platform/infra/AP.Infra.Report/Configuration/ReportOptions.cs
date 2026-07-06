namespace AP.Infra.Report.Configuration;

/// <summary>
/// 报表框架配置
/// </summary>
public class ReportOptions
{
    /// <summary>
    /// 配置节名称
    /// </summary>
    public const string SectionName = "Report";

    /// <summary>
    /// 存储配置
    /// </summary>
    public StorageOptions Storage { get; set; } = new();

    /// <summary>
    /// 归档配置
    /// </summary>
    public ArchiveOptions Archive { get; set; } = new();

    /// <summary>
    /// 保留策略配置
    /// </summary>
    public RetentionOptions Retention { get; set; } = new();

    /// <summary>
    /// 清理配置
    /// </summary>
    public CleanupOptions Cleanup { get; set; } = new();
}

/// <summary>
/// 存储配置
/// </summary>
public class StorageOptions
{
    /// <summary>
    /// 报表存储根目录
    /// </summary>
    public string RootPath { get; set; } = "reports";

    /// <summary>
    /// 路径格式模板
    /// 支持变量: {year}, {month}, {date}, {type}
    /// </summary>
    public string PathFormat { get; set; } = "{year}/{month}/{date}_{type}.xlsx";

    /// <summary>
    /// 默认模板路径（可选）
    /// </summary>
    public string? DefaultTemplatePath { get; set; }
}

/// <summary>
/// 归档配置
/// </summary>
public class ArchiveOptions
{
    /// <summary>
    /// 是否启用定时归档
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 归档执行时间（每天几点执行，格式 HH:mm）
    /// </summary>
    public string Time { get; set; } = "02:00";

    /// <summary>
    /// 归档的报表类型列表
    /// 为空则归档所有已注册的报表类型
    /// </summary>
    public List<string> ReportTypes { get; set; } = [];
}

/// <summary>
/// 保留策略配置
/// </summary>
public class RetentionOptions
{
    /// <summary>
    /// 是否启用保留策略
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 保留天数
    /// </summary>
    public int Days { get; set; } = 180;

    /// <summary>
    /// 清理检查间隔（ TimeSpan 格式，如 "1.00:00:00" 表示每天）
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// 清理时是否删除文件（false 则仅删除数据库记录）
    /// </summary>
    public bool DeleteFiles { get; set; } = true;

    /// <summary>
    /// 受保护的报表类型（不会被自动清理）
    /// </summary>
    public List<string> ProtectedTypes { get; set; } = [];
}

/// <summary>
/// 清理配置
/// </summary>
public class CleanupOptions
{
    /// <summary>
    /// 是否启用清理功能
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 模拟运行模式（true = 只记录不删除）
    /// </summary>
    public bool DryRun { get; set; } = false;
}