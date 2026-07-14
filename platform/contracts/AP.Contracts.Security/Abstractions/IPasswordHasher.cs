namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 密码哈希服务
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 对明文密码进行哈希
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// 验证明文密码与哈希是否匹配
    /// </summary>
    bool VerifyPassword(string password, string hashedPassword);
}
