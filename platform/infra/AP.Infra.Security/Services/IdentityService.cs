using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;
using AP.Infra.Security.Configuration;
using AP.Shared.Utilities.Security;
using Microsoft.Extensions.Options;

namespace AP.Infra.Security.Services;

/// <summary>
/// 单机版身份认证服务实现
/// </summary>
public class IdentityService : IIdentityService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly SecurityOptions _options;

    public IdentityService(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOptions<SecurityOptions>? options = null)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
        _options = options?.Value ?? new SecurityOptions();
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

        var lockoutUntil = await _userRepository.GetLockoutUntilAsync(user.Id, ct);
        if (lockoutUntil is { } until && until > DateTime.Now)
        {
            var remainingMinutes = Math.Max(1, (int)Math.Ceiling((until - DateTime.Now).TotalMinutes));
            return LoginResult.Fail($"账号已锁定，请在 {remainingMinutes} 分钟后再试");
        }

        var passwordHash = await _userRepository.GetPasswordHashAsync(user.Id, ct);
        if (string.IsNullOrEmpty(passwordHash) || !_passwordHasher.VerifyPassword(request.Password, passwordHash))
        {
            await _userRepository.RegisterFailedLoginAsync(
                user.Id,
                _options.MaxFailedLoginAttempts,
                TimeSpan.FromMinutes(_options.LockoutMinutes),
                ct);
            return LoginResult.Fail("用户名或密码错误");
        }

        await _userRepository.ResetFailedLoginAsync(user.Id, ct);
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
        var policy = PasswordPolicy.Validate(request.NewPassword);
        if (!policy.Succeeded)
            return (false, policy.Message);

        var user = await _userRepository.GetByUserNameAsync(request.UserName, ct);
        if (user == null)
            return (false, "用户不存在");

        var passwordHash = await _userRepository.GetPasswordHashAsync(user.Id, ct);
        if (string.IsNullOrEmpty(passwordHash) || !_passwordHasher.VerifyPassword(request.CurrentPassword, passwordHash))
            return (false, "当前密码错误");

        var newHash = _passwordHasher.HashPassword(request.NewPassword);
        await _userRepository.UpdatePasswordAsync(user.Id, newHash, ct);

        if (user.MustChangePassword)
        {
            user.MustChangePassword = false;
            await _userRepository.UpdateAsync(user, ct);
        }

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
