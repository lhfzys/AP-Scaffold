using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;
using AP.Infra.Security.Entities;
using FreeSql;

namespace AP.Infra.Security.Repositories;

/// <summary>
/// 用户仓储实现
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IFreeSql _freeSql;

    public UserRepository(IFreeSql freeSql)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
    }

    public async Task<UserInfo?> GetByUserNameAsync(string userName, CancellationToken ct = default)
    {
        var user = await _freeSql.Select<User>()
            .Where(u => u.UserName == userName)
            .ToOneAsync(ct);

        return user == null ? null : await LoadUserRolesAndMapAsync(user, ct);
    }

    public async Task<UserInfo?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await _freeSql.Select<User>()
            .Where(u => u.Id == id)
            .ToOneAsync(ct);

        return user == null ? null : await LoadUserRolesAndMapAsync(user, ct);
    }

    public async Task<IReadOnlyList<UserInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _freeSql.Select<User>().ToListAsync(ct);

        var result = new List<UserInfo>();
        foreach (var user in users)
        {
            result.Add(await LoadUserRolesAndMapAsync(user, ct));
        }

        return result;
    }

    public async Task CreateAsync(UserInfo user, string passwordHash, CancellationToken ct = default)
    {
        var entity = new User
        {
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            PasswordHash = passwordHash,
            IsEnabled = user.IsEnabled,
            MustChangePassword = user.MustChangePassword
        };

        var userId = await _freeSql.Insert(entity).ExecuteIdentityAsync(ct);
        await BindRolesAsync(userId, user.Roles, ct);
    }

    public async Task UpdateAsync(UserInfo user, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<User>().Where(u => u.Id == user.Id).ToOneAsync(ct)
            ?? throw new InvalidOperationException($"用户 {user.Id} 不存在");

        entity.UserName = user.UserName;
        entity.DisplayName = user.DisplayName;
        entity.IsEnabled = user.IsEnabled;
        entity.MustChangePassword = user.MustChangePassword;

        await _freeSql.Update<User>().SetSource(entity).ExecuteAffrowsAsync(ct);

        // 重新绑定用户角色
        await _freeSql.Delete<UserRole>().Where(ur => ur.UserId == user.Id).ExecuteAffrowsAsync(ct);
        await BindRolesAsync(user.Id, user.Roles, ct);
    }

    public async Task UpdatePasswordAsync(long id, string passwordHash, CancellationToken ct = default)
    {
        await _freeSql.Update<User>(id)
            .Set(a => a.PasswordHash, passwordHash)
            .ExecuteAffrowsAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        await _freeSql.Delete<User>(id).ExecuteAffrowsAsync(ct);
    }

    public async Task<string?> GetPasswordHashAsync(long id, CancellationToken ct = default)
    {
        var user = await _freeSql.Select<User>().Where(u => u.Id == id).ToOneAsync(ct);
        return user?.PasswordHash;
    }

    /// <summary>
    /// 手动加载用户角色与权限，避免 FreeSql IncludeMany 在 ManyToMany 导航上的解析问题
    /// </summary>
    private async Task<UserInfo> LoadUserRolesAndMapAsync(User user, CancellationToken ct)
    {
        var roleIds = await _freeSql.Select<UserRole>()
            .Where(ur => ur.UserId == user.Id)
            .ToListAsync(ur => ur.RoleId, ct);

        if (roleIds.Count == 0)
            return MapToInfo(user, Array.Empty<Role>(), Array.Empty<Permission>());

        var roles = await _freeSql.Select<Role>()
            .Where(r => roleIds.Contains(r.Id))
            .ToListAsync(ct);

        var permissionIds = await _freeSql.Select<RolePermission>()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .ToListAsync(rp => rp.PermissionId, ct);

        List<Permission> permissions;
        if (permissionIds.Count == 0)
        {
            permissions = new List<Permission>();
        }
        else
        {
            permissions = await _freeSql.Select<Permission>()
                .Where(p => permissionIds.Contains(p.Id))
                .ToListAsync(ct);
        }

        return MapToInfo(user, roles, permissions);
    }

    private async Task BindRolesAsync(long userId, IEnumerable<string> roleNames, CancellationToken ct)
    {
        var names = roleNames?.ToList() ?? new List<string>();
        if (names.Count == 0) return;

        var roles = await _freeSql.Select<Role>()
            .Where(r => names.Contains(r.Name))
            .ToListAsync(ct);

        foreach (var role in roles)
        {
            await _freeSql.Insert(new UserRole { UserId = userId, RoleId = role.Id }).ExecuteAffrowsAsync(ct);
        }
    }

    private static UserInfo MapToInfo(User user, IEnumerable<Role> roles, IEnumerable<Permission> permissions)
    {
        var roleNames = roles.Select(r => r.Name).ToList();
        var permissionCodes = permissions.Select(p => p.Code).Distinct().ToList();

        return new UserInfo
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            IsEnabled = user.IsEnabled,
            MustChangePassword = user.MustChangePassword,
            Roles = roleNames,
            Permissions = permissionCodes
        };
    }
}
