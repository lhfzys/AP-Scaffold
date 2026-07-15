using AP.Contracts.Security.Audit;
using FreeSql;

namespace AP.Infra.Security.Audit;

/// <summary>
/// 审计日志服务实现
/// </summary>
public class AuditService : IAuditService
{
    private readonly IFreeSql _freeSql;

    public AuditService(IFreeSql freeSql)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        var entity = new AuditLog
        {
            Timestamp = entry.Timestamp,
            UserName = entry.UserName,
            ActionType = entry.ActionType,
            ActionName = entry.ActionName,
            TargetId = entry.TargetId,
            Description = entry.Description,
            IpAddress = entry.IpAddress,
            Succeeded = entry.Succeeded,
            ErrorMessage = entry.ErrorMessage
        };

        await _freeSql.Insert(entity).ExecuteAffrowsAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> QueryAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null,
        int skip = 0,
        int take = 100,
        CancellationToken ct = default)
    {
        var query = BuildQuery(startTime, endTime, userName, actionType);
        var logs = await query.OrderByDescending(a => a.Timestamp).Page(skip / take + 1, take).ToListAsync(ct);

        return logs.Select(MapToEntry).ToList();
    }

    public async Task<int> CountAsync(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null,
        CancellationToken ct = default)
    {
        var query = BuildQuery(startTime, endTime, userName, actionType);
        return (int)await query.CountAsync(ct);
    }

    private ISelect<AuditLog> BuildQuery(
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? userName = null,
        AuditActionType? actionType = null)
    {
        var query = _freeSql.Select<AuditLog>();

        if (startTime.HasValue)
            query = query.Where(a => a.Timestamp >= startTime.Value);

        if (endTime.HasValue)
            query = query.Where(a => a.Timestamp <= endTime.Value);

        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(a => a.UserName == userName);

        if (actionType.HasValue)
            query = query.Where(a => a.ActionType == actionType.Value);

        return query;
    }

    private static AuditLogEntry MapToEntry(AuditLog log)
    {
        return new AuditLogEntry
        {
            Id = log.Id,
            Timestamp = log.Timestamp,
            UserName = log.UserName,
            ActionType = log.ActionType,
            ActionName = log.ActionName,
            TargetId = log.TargetId,
            Description = log.Description,
            IpAddress = log.IpAddress,
            Succeeded = log.Succeeded,
            ErrorMessage = log.ErrorMessage
        };
    }
}
