using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.Login.ViewModels;

/// <summary>
/// 修改密码窗口 ViewModel
/// </summary>
public partial class ChangePasswordViewModel : ViewModelBase
{
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ILogger<ChangePasswordViewModel> _logger;

    /// <summary>
    /// 需要修改密码的用户名
    /// </summary>
    public string UserName { get; set; } = string.Empty;

    [ObservableProperty]
    private string _currentPassword = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// 是否修改成功
    /// </summary>
    public bool IsChanged { get; private set; }

    public ChangePasswordViewModel(
        IIdentityService identityService,
        IAuditService auditService,
        ILogger<ChangePasswordViewModel> logger)
    {
        _identityService = identityService ?? throw new ArgumentNullException(nameof(identityService));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private async Task ChangePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "请输入当前密码";
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            ErrorMessage = "请输入新密码";
            return;
        }

        if (NewPassword != ConfirmPassword)
        {
            ErrorMessage = "两次输入的新密码不一致";
            return;
        }

        ErrorMessage = string.Empty;
        BusyText = "正在修改密码...";
        IsBusy = true;

        try
        {
            var result = await _identityService.ChangePasswordAsync(new ChangePasswordRequest
            {
                UserName = UserName,
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword
            });

            if (!result.Succeeded)
            {
                ErrorMessage = result.Message;
                await LogAuditAsync(false, result.Message);
                return;
            }

            IsChanged = true;
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
        IsChanged = false;
        OnRequestClose();
    }

    private async Task LogAuditAsync(bool succeeded, string? error = null)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = UserName,
                ActionType = AuditActionType.PasswordChanged,
                ActionName = "修改密码",
                Succeeded = succeeded,
                ErrorMessage = error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录改密审计日志失败");
        }
    }
}
