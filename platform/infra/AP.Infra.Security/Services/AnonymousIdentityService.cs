using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;

namespace AP.Infra.Security.Services;

/// <summary>
/// 匿名身份服务（安全模块禁用时使用）
/// 始终返回一个拥有全部权限的虚拟管理员，保证业务插件注入 IIdentityService 不抛异常
/// </summary>
public class AnonymousIdentityService : IIdentityService
{
    private static readonly UserInfo AnonymousAdmin = new()
    {
        Id = 0,
        UserName = "anonymous",
        DisplayName = "系统用户",
        IsEnabled = true,
        Roles = ["Administrator"],
        Permissions = ["*"]
    };

    public UserInfo? CurrentUser => AnonymousAdmin;

    public Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
        => Task.FromResult(LoginResult.Success(AnonymousAdmin));

    public Task LogoutAsync(CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<(bool Succeeded, string Message)> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
        => Task.FromResult((false, "安全模块已禁用，无法修改密码"));

    public bool HasPermission(string permissionCode) => true;

    public bool IsInRole(string roleName) => true;
}
