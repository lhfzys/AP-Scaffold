using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;
using AP.Infra.Security.Entities;
using FreeSql;

namespace AP.Infra.Security.Repositories;

/// <summary>
/// 角色仓储实现
/// </summary>
public class RoleRepository : IRoleRepository
{
    private readonly IFreeSql _freeSql;

    public RoleRepository(IFreeSql freeSql)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
    }

    public async Task<RoleInfo?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var role = await _freeSql.Select<Role>().Where(r => r.Id == id).ToOneAsync(ct);
        if (role == null) return null;

        return await LoadPermissionsAndMapAsync(role, ct);
    }

    public async Task<RoleInfo?> GetByNameAsync(string name, CancellationToken ct = default)
    {
        var role = await _freeSql.Select<Role>().Where(r => r.Name == name).ToOneAsync(ct);
        if (role == null) return null;

        return await LoadPermissionsAndMapAsync(role, ct);
    }

    public async Task<IReadOnlyList<RoleInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var roles = await _freeSql.Select<Role>().OrderBy(r => r.Id).ToListAsync(ct);

        var result = new List<RoleInfo>();
        foreach (var role in roles)
        {
            result.Add(await LoadPermissionsAndMapAsync(role, ct));
        }

        return result;
    }

    public async Task CreateAsync(RoleInfo role, CancellationToken ct = default)
    {
        var entity = new Role
        {
            Name = role.Name,
            Description = role.Description
        };

        var roleId = await _freeSql.Insert(entity).ExecuteIdentityAsync(ct);

        await BindPermissionsAsync(roleId, role.Permissions, ct);
    }

    public async Task UpdateAsync(RoleInfo role, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<Role>().Where(r => r.Id == role.Id).ToOneAsync(ct)
            ?? throw new InvalidOperationException($"角色 {role.Id} 不存在");

        entity.Name = role.Name;
        entity.Description = role.Description;

        await _freeSql.Update<Role>().SetSource(entity).ExecuteAffrowsAsync(ct);

        // 重新绑定权限
        await _freeSql.Delete<RolePermission>().Where(rp => rp.RoleId == role.Id).ExecuteAffrowsAsync(ct);
        await BindPermissionsAsync(role.Id, role.Permissions, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await _freeSql.Delete<RolePermission>().Where(rp => rp.RoleId == id).ExecuteAffrowsAsync(ct);
        await _freeSql.Delete<UserRole>().Where(ur => ur.RoleId == id).ExecuteAffrowsAsync(ct);
        await _freeSql.Delete<Role>(id).ExecuteAffrowsAsync(ct);
    }

    private async Task<RoleInfo> LoadPermissionsAndMapAsync(Role role, CancellationToken ct)
    {
        var permissionIds = await _freeSql.Select<RolePermission>()
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(rp => rp.PermissionId, ct);

        List<string> permissionCodes;
        if (permissionIds.Count == 0)
        {
            permissionCodes = new List<string>();
        }
        else
        {
            permissionCodes = await _freeSql.Select<Permission>()
                .Where(p => permissionIds.Contains(p.Id))
                .ToListAsync(p => p.Code, ct);
        }

        return MapToInfo(role, permissionCodes);
    }

    private async Task BindPermissionsAsync(long roleId, IEnumerable<string> permissionCodes, CancellationToken ct)
    {
        var codes = permissionCodes?.ToList() ?? new List<string>();
        if (codes.Count == 0) return;

        var permissions = await _freeSql.Select<Permission>()
            .Where(p => codes.Contains(p.Code))
            .ToListAsync(ct);

        foreach (var permission in permissions)
        {
            await _freeSql.Insert(new RolePermission { RoleId = roleId, PermissionId = permission.Id }).ExecuteAffrowsAsync(ct);
        }
    }

    private static RoleInfo MapToInfo(Role role, IReadOnlyList<string> permissionCodes)
    {
        return new RoleInfo
        {
            Id = role.Id,
            Name = role.Name,
            Description = role.Description,
            Permissions = permissionCodes
        };
    }
}
