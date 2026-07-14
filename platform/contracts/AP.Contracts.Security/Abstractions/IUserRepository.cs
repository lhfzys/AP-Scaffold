using AP.Contracts.Security.Models;

namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 用户仓储
/// </summary>
public interface IUserRepository
{
    Task<UserInfo?> GetByUserNameAsync(string userName, CancellationToken ct = default);

    Task<UserInfo?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<UserInfo>> GetAllAsync(CancellationToken ct = default);

    Task CreateAsync(UserInfo user, string passwordHash, CancellationToken ct = default);

    Task UpdateAsync(UserInfo user, CancellationToken ct = default);

    Task UpdatePasswordAsync(long id, string passwordHash, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 获取用户密码哈希（用于本地认证，不对外暴露）
    /// </summary>
    Task<string?> GetPasswordHashAsync(long id, CancellationToken ct = default);
}
