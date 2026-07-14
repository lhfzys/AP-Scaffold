using AP.Contracts.Security.Audit;
using AP.Infra.Database.Entities;
using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Audit;

/// <summary>
/// 审计日志数据库实体
/// </summary>
[Table(Name = "sys_audit_logs")]
public class AuditLog : BaseEntity
{
    /// <summary>
    /// 发生时间
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// 操作人
    /// </summary>
    [Column(StringLength = 50)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 操作类型
    /// </summary>
    public AuditActionType ActionType { get; set; }

    /// <summary>
    /// 操作名称
    /// </summary>
    [Column(StringLength = 100)]
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// 操作对象标识
    /// </summary>
    [Column(StringLength = 200)]
    public string? TargetId { get; set; }

    /// <summary>
    /// 操作说明
    /// </summary>
    [Column(StringLength = 2000)]
    public string? Description { get; set; }

    /// <summary>
    /// IP 地址
    /// </summary>
    [Column(StringLength = 50)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// 是否成功
    /// </summary>
    public bool Succeeded { get; set; }

    /// <summary>
    /// 错误信息
    /// </summary>
    [Column(StringLength = 2000)]
    public string? ErrorMessage { get; set; }
}
