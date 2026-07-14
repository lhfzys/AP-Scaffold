using AP.Contracts.Security.Audit;

namespace AP.Infra.Security.Audit;

/// <summary>
/// 空审计服务（审计禁用时使用）
/// </summary>
public class NullAuditService : IAuditService
{
    public Task LogAsync(AuditLogEntry entry, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AuditLogEntry>>(new List<AuditLogEntry>());
}
