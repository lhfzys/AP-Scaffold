namespace AP.Contracts.Security.Audit;

/// <summary>
/// 审计日志条目
/// </summary>
public class AuditLogEntry
{
    public long Id { get; set; }

    public DateTime Timestamp { get; set; }

    public string UserName { get; set; } = string.Empty;

    public AuditActionType ActionType { get; set; }

    public string ActionName { get; set; } = string.Empty;

    public string? TargetId { get; set; }

    public string? Description { get; set; }

    public string? IpAddress { get; set; }

    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }
}
