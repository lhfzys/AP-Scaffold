using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;

namespace AP.Infra.Security.Services;

/// <summary>
/// 单机版身份认证服务实现
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public IdentityService(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public UserInfo? CurrentUser { get; private set; }

    public async Task<LoginResult> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            return LoginResult.Fail("用户名或密码不能为空");

        var user = await _userRepository.GetByUserNameAsync(request.UserName, ct);
        if (user == null)
            return LoginResult.Fail("用户名或密码错误");

        if (!user.IsEnabled)
            return LoginResult.Fail("用户已被禁用");

        var passwordHash = await _userRepository.GetPasswordHashAsync(user.Id, ct);
        if (string.IsNullOrEmpty(passwordHash) || !_passwordHasher.VerifyPassword(request.Password, passwordHash))
            return LoginResult.Fail("用户名或密码错误");

        CurrentUser = user;
        return LoginResult.Success(user);
    }

    public Task LogoutAsync(CancellationToken ct = default)
    {
        CurrentUser = null;
        return Task.CompletedTask;
    }

    public async Task<(bool Succeeded, string Message)> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return (false, "新密码不能为空");

        var user = await _userRepository.GetByUserNameAsync(request.UserName, ct);
        if (user == null)
            return (false, "用户不存在");

        var passwordHash = await _userRepository.GetPasswordHashAsync(user.Id, ct);
        if (string.IsNullOrEmpty(passwordHash) || !_passwordHasher.VerifyPassword(request.CurrentPassword, passwordHash))
            return (false, "当前密码错误");

        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(user.Id, newHash, ct);

        return (true, "密码修改成功");
    }

    public bool HasPermission(string permissionCode)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return false;

        return CurrentUser?.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase) ?? false;
    }

    public bool IsInRole(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
            return false;

        return CurrentUser?.Roles.Contains(roleName, StringComparer.OrdinalIgnoreCase) ?? false;
    }
}
