namespace AP.Contracts.Security.Models;

/// <summary>
/// 用户信息（脱敏，不含密码哈希）
/// </summary>
public class UserInfo
{
    public long Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>
    /// 首次登录后是否必须修改密码
    /// </summary>
    public bool MustChangePassword { get; set; }

    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
