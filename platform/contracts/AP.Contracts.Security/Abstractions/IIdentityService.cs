using AP.Contracts.Security.Models;

namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 身份认证服务
/// </summary>
public interface IIdentityService
{
    /// <summary>
    /// 当前登录用户，未登录时为 null
    /// </summary>
    UserInfo? CurrentUser { get; }

    /// <summary>
    /// 登录
    /// </summary>
    Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>
    /// 登出
    /// </summary>
    Task LogoutAsync(CancellationToken ct = default);

    /// <summary>
    /// 修改密码
    /// </summary>
    Task<(bool Succeeded, string Message)> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default);

    /// <summary>
    /// 检查当前用户是否拥有指定权限
    /// </summary>
    bool HasPermission(string permissionCode);

    /// <summary>
    /// 检查当前用户是否属于指定角色
    /// </summary>
    bool IsInRole(string roleName);
}
