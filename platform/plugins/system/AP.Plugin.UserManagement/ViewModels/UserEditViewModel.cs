#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using System.Linq;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.UserManagement.ViewModels;

/// <summary>
/// 用户编辑窗口 ViewModel
/// </summary>
public partial class UserEditViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<UserEditViewModel> _logger;

    [ObservableProperty]
    private string _title = "新增用户";

    [ObservableProperty]
    private long _userId;

    [ObservableProperty]
    private string _userName = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _mustChangePassword = true;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _confirmPassword = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _availableRoles = new();

    [ObservableProperty]
    private ObservableCollection<string> _selectedRoles = new();

    /// <summary>
    /// 是否保存成功
    /// </summary>
    public bool IsSaved { get; private set; }

    private bool _isEdit;

    public UserEditViewModel(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IPasswordHasher passwordHasher,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        ILogger<UserEditViewModel> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _passwordHasher = passwordHasher;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeForCreateAsync()
    {
        IsSaved = false;
        _isEdit = false;
        Title = "新增用户";
        UserId = 0;
        UserName = string.Empty;
        DisplayName = string.Empty;
        IsEnabled = true;
        MustChangePassword = true;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        await LoadRolesAsync();
        SelectedRoles = new ObservableCollection<string>();
    }

    public async Task InitializeForEditAsync(UserInfo user)
    {
        IsSaved = false;
        _isEdit = true;
        Title = "编辑用户";
        UserId = user.Id;
        UserName = user.UserName;
        DisplayName = user.DisplayName;
        IsEnabled = user.IsEnabled;
        MustChangePassword = user.MustChangePassword;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        await LoadRolesAsync();
        SelectedRoles = new ObservableCollection<string>(user.Roles);
    }

    private async Task LoadRolesAsync()
    {
        var roles = await _roleRepository.GetAllAsync();
        AvailableRoles = new ObservableCollection<string>(roles.Select(r => r.Name).OrderBy(n => n));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(UserName))
        {
            await _dialogService.ShowErrorAsync("用户名不能为空");
            return;
        }

        if (!_isEdit && string.IsNullOrWhiteSpace(Password))
        {
            await _dialogService.ShowErrorAsync("新增用户时必须设置初始密码");
            return;
        }

        if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
        {
            await _dialogService.ShowErrorAsync("两次输入的密码不一致");
            return;
        }

        IsBusy = true;
        try
        {
            var userInfo = new UserInfo
            {
                Id = UserId,
                UserName = UserName.Trim(),
                DisplayName = DisplayName.Trim(),
                IsEnabled = IsEnabled,
                MustChangePassword = MustChangePassword,
                Roles = SelectedRoles.ToList()
            };

            if (_isEdit)
            {
                await _userRepository.UpdateAsync(userInfo);
                if (!string.IsNullOrEmpty(Password))
                {
                    await _userRepository.UpdatePasswordAsync(UserId, _passwordHasher.HashPassword(Password));
                }

                await LogAuditAsync(AuditActionType.Update, userInfo.UserName, true, "编辑用户");
            }
            else
            {
                var existing = await _userRepository.GetByUserNameAsync(userInfo.UserName);
                if (existing != null)
                {
                    await _dialogService.ShowErrorAsync("用户名已存在");
                    return;
                }

                await _userRepository.CreateAsync(userInfo, _passwordHasher.HashPassword(Password));
                await LogAuditAsync(AuditActionType.Create, userInfo.UserName, true, "新增用户");
            }

            IsSaved = true;
            OnRequestClose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存用户失败");
            await _dialogService.ShowErrorAsync("保存用户失败：" + ex.Message);
            await LogAuditAsync(_isEdit ? AuditActionType.Update : AuditActionType.Create, UserName, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        IsSaved = false;
        OnRequestClose();
    }

    private async Task LogAuditAsync(AuditActionType actionType, string userName, bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = _identityService.CurrentUser?.UserName ?? "unknown",
                ActionType = actionType,
                ActionName = description,
                TargetId = userName,
                Succeeded = succeeded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录审计日志失败");
        }
    }
}
