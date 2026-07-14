namespace AP.Contracts.Security.Models;

/// <summary>
/// 登录结果
/// </summary>
public class LoginResult
{
    public bool Succeeded { get; set; }

    public string Message { get; set; } = string.Empty;

    public UserInfo? User { get; set; }

    public static LoginResult Success(UserInfo user) => new() { Succeeded = true, User = user };

    public static LoginResult Fail(string message) => new() { Succeeded = false, Message = message };
}
