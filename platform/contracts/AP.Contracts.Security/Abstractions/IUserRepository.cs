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

    /// <summary>
    /// 获取账号锁定截止时间；null 表示未锁定。
    /// </summary>
    Task<DateTime?> GetLockoutUntilAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 记录一次登录失败；达到阈值时写入锁定截止时间。
    /// </summary>
    Task RegisterFailedLoginAsync(long id, int maxFailedAttempts, TimeSpan lockoutDuration, CancellationToken ct = default);

    /// <summary>
    /// 登录成功后清除失败次数与锁定时间。
    /// </summary>
    Task ResetFailedLoginAsync(long id, CancellationToken ct = default);
}
