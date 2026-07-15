using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.Login.ViewModels;

/// <summary>
/// 登录窗口 ViewModel
/// </summary>
public partial class LoginViewModel : ViewModelBase
{
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    private string _userName = "admin";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 是否已认证成功
    /// </summary>
    public bool IsAuthenticated { get; private set; }

    public LoginViewModel(
        IIdentityService identityService,
        IAuditService auditService,
        ILogger<LoginViewModel> logger)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "用户名或密码不能为空";
            return;
        }

        ErrorMessage = string.Empty;
        IsBusy = true;

        try
        {
            var result = await _identityService.LoginAsync(new LoginRequest
            {
                UserName = UserName.Trim(),
                Password = Password
            });

            if (!result.Succeeded)
            {
                ErrorMessage = result.Message;
                await LogAuditAsync(false, result.Message);
                return;
            }

            IsAuthenticated = true;
            await LogAuditAsync(true);
            OnRequestClose();
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        IsAuthenticated = false;
        OnRequestClose();
    }

    private async Task LogAuditAsync(bool succeeded, string? error = null)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = UserName.Trim(),
                ActionType = AuditActionType.Login,
                ActionName = "用户登录",
                Succeeded = succeeded,
                ErrorMessage = error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录登录审计日志失败");
        }
    }
}
