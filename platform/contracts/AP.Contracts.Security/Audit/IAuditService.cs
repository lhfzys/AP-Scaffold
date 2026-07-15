namespace AP.Contracts.Security.Audit;

/// <summary>
/// 审计日志服务
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// 记录审计日志
    /// </summary>
    Task LogAsync(AuditLogEntry entry, CancellationToken ct = default);

    /// <summary>
    /// 查询审计日志
    /// </summary>
    Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default);

    /// <summary>
    /// 统计审计日志数量
    /// </summary>
    Task<int> CountAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null,
        CancellationToken ct = default);
}
