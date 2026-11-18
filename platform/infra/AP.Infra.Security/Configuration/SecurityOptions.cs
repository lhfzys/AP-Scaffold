namespace AP.Infra.Security.Configuration;

/// <summary>
/// 本地登录安全选项（Security 配置节）。
/// </summary>
public class SecurityOptions
{
    /// <summary>连续失败达到该次数后锁定账号。</summary>
    public int MaxFailedLoginAttempts { get; set; } = 5;

    /// <summary>锁定分钟数。</summary>
    public int LockoutMinutes { get; set; } = 5;
}
