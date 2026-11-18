using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Models;
using AP.Infra.Security.Configuration;
using AP.Infra.Security.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Security;

public class IdentityServiceTests
{
    private static readonly UserInfo Admin = new()
    {
        Id = 1,
        UserName = "admin",
        DisplayName = "系统管理员",
        IsEnabled = true
    };

    private static (IdentityService Service, IUserRepository Repository, IPasswordHasher Hasher) Create()
    {
        var repository = Substitute.For<IUserRepository>();
        var hasher = Substitute.For<IPasswordHasher>();
        var options = Options.Create(new SecurityOptions
        {
            MaxFailedLoginAttempts = 3,
            LockoutMinutes = 5
        });
        return (new IdentityService(repository, hasher, options), repository, hasher);
    }

    [Fact]
    public async Task Login_WithWrongPassword_RecordsFailureAndDoesNotReset()
    {
        var (service, repository, hasher) = Create();
        repository.GetByUserNameAsync("admin", Arg.Any<CancellationToken>()).Returns(Task.FromResult<UserInfo?>(Admin));
        repository.GetLockoutUntilAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateTime?>(null));
        repository.GetPasswordHashAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("hash"));
        hasher.VerifyPassword("bad", "hash").Returns(false);

        var result = await service.LoginAsync(new LoginRequest { UserName = "admin", Password = "bad" });

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Be("用户名或密码错误");
        await repository.Received(1).RegisterFailedLoginAsync(
            1, 3, TimeSpan.FromMinutes(5), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().ResetFailedLoginAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Login_WhenLocked_DoesNotVerifyPassword()
    {
        var (service, repository, hasher) = Create();
        repository.GetByUserNameAsync("admin", Arg.Any<CancellationToken>()).Returns(Task.FromResult<UserInfo?>(Admin));
        repository.GetLockoutUntilAsync(1, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DateTime?>(DateTime.Now.AddMinutes(3)));

        var result = await service.LoginAsync(new LoginRequest { UserName = "admin", Password = "x" });

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("锁定");
        hasher.DidNotReceive().VerifyPassword(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Login_WithValidPassword_ResetsFailuresAndSetsCurrentUser()
    {
        var (service, repository, hasher) = Create();
        repository.GetByUserNameAsync("admin", Arg.Any<CancellationToken>()).Returns(Task.FromResult<UserInfo?>(Admin));
        repository.GetLockoutUntilAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<DateTime?>(null));
        repository.GetPasswordHashAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("hash"));
        hasher.VerifyPassword("Admin123", "hash").Returns(true);

        var result = await service.LoginAsync(new LoginRequest { UserName = "admin", Password = "Admin123" });

        result.Succeeded.Should().BeTrue();
        service.CurrentUser.Should().BeSameAs(Admin);
        await repository.Received(1).ResetFailedLoginAsync(1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_WithWeakPassword_IsRejectedBeforeRepositoryWrite()
    {
        var (service, repository, _) = Create();

        var result = await service.ChangePasswordAsync(new ChangePasswordRequest
        {
            UserName = "admin",
            CurrentPassword = "Admin123",
            NewPassword = "1"
        });

        result.Succeeded.Should().BeFalse();
        result.Message.Should().Contain("至少");
        await repository.DidNotReceive().UpdatePasswordAsync(Arg.Any<long>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangePassword_WithValidPassword_ClearsMustChangeFlag()
    {
        var (service, repository, hasher) = Create();
        var user = new UserInfo
        {
            Id = 1,
            UserName = "admin",
            IsEnabled = true,
            MustChangePassword = true
        };

        repository.GetByUserNameAsync("admin", Arg.Any<CancellationToken>()).Returns(Task.FromResult<UserInfo?>(user));
        repository.GetPasswordHashAsync(1, Arg.Any<CancellationToken>()).Returns(Task.FromResult<string?>("old-hash"));
        hasher.VerifyPassword("Admin123", "old-hash").Returns(true);

        var result = await service.ChangePasswordAsync(new ChangePasswordRequest
        {
            UserName = "admin",
            CurrentPassword = "Admin123",
            NewPassword = "NewAdmin123"
        });

        result.Succeeded.Should().BeTrue();
        await repository.Received(1).UpdatePasswordAsync(1, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await repository.Received(1).UpdateAsync(
            Arg.Is<UserInfo>(u => u.Id == 1 && !u.MustChangePassword),
            Arg.Any<CancellationToken>());
    }
}
