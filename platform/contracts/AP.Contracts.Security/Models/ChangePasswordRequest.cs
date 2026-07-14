namespace AP.Contracts.Security.Models;

/// <summary>
/// 修改密码请求
/// </summary>
public class ChangePasswordRequest
{
    public string UserName { get; set; } = string.Empty;

    public string CurrentPassword { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
