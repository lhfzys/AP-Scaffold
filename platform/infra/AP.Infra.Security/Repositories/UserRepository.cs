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
            .IncludeMany(u => u.Roles, then => then.Include(r => r.Permissions))
            .ToOneAsync(ct);

        return user == null ? null : MapToInfo(user);
    }

    public async Task<UserInfo?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await _freeSql.Select<User>()
            .Where(u => u.Id == id)
            .IncludeMany(u => u.Roles, then => then.Include(r => r.Permissions))
            .ToOneAsync(ct);

        return user == null ? null : MapToInfo(user);
    }

    public async Task<IReadOnlyList<UserInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _freeSql.Select<User>()
            .IncludeMany(u => u.Roles, then => then.Include(r => r.Permissions))
            .ToListAsync(ct);

        return users.Select(MapToInfo).ToList();
    }

    public async Task CreateAsync(UserInfo user, string passwordHash, CancellationToken ct = default)
    {
        var entity = new User
        {
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            PasswordHash = passwordHash,
            IsEnabled = user.IsEnabled
        };

        await _freeSql.Insert(entity).ExecuteIdentityAsync(ct);
    }

    public async Task UpdateAsync(UserInfo user, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<User>().Where(u => u.Id == user.Id).ToOneAsync(ct)
            ?? throw new InvalidOperationException($"用户 {user.Id} 不存在");

        entity.UserName = user.UserName;
        entity.DisplayName = user.DisplayName;
        entity.IsEnabled = user.IsEnabled;

        await _freeSql.Update<User>().SetSource(entity).ExecuteAffrowsAsync(ct);
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

    private static UserInfo MapToInfo(User user)
    {
        var roles = user.Roles.Select(r => r.Name).ToList();
        var permissions = user.Roles
            .SelectMany(r => r.Permissions)
            .Select(p => p.Code)
            .Distinct()
            .ToList();

        return new UserInfo
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            IsEnabled = user.IsEnabled,
            Roles = roles,
            Permissions = permissions
        };
    }
}
