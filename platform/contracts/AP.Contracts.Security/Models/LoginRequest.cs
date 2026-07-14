namespace AP.Contracts.Security.Models;

/// <summary>
/// 登录请求
/// </summary>
public class LoginRequest
{
    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;
}
