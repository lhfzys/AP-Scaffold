using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;
using AP.Infra.Security.Entities;
using FreeSql;

namespace AP.Infra.Security.Repositories;

/// <summary>
/// 权限仓储实现
/// </summary>
public class PermissionRepository : IPermissionRepository
{
    private readonly IFreeSql _freeSql;

    public PermissionRepository(IFreeSql freeSql)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
    }

    public async Task<IReadOnlyList<PermissionInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var permissions = await _freeSql.Select<Permission>()
            .OrderBy(p => p.Code)
            .ToListAsync(ct);

        return permissions.Select(MapToInfo).ToList();
    }

    public async Task<PermissionInfo?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var permission = await _freeSql.Select<Permission>()
            .Where(p => p.Code == code)
            .ToOneAsync(ct);

        return permission == null ? null : MapToInfo(permission);
    }

    private static PermissionInfo MapToInfo(Permission permission)
    {
        return new PermissionInfo
        {
            Id = permission.Id,
            Code = permission.Code,
            Name = permission.Name
        };
    }
}
