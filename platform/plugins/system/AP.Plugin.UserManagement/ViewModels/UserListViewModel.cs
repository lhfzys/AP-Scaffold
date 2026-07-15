#region

using System.Collections.ObjectModel;
using System.Windows;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prism.Navigation;

#endregion

namespace AP.Plugin.UserManagement.ViewModels;

/// <summary>
/// 用户列表 ViewModel
/// </summary>
public partial class UserListViewModel : ViewModelBase
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UserListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<UserInfo> _users = new();

    [ObservableProperty]
    private UserInfo? _selectedUser;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public UserListViewModel(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<UserListViewModel> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;

        Title = "用户管理";
    }

    public override async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await LoadUsersAsync();
    }

    [RelayCommand]
    private async Task LoadUsersAsync()
    {
        IsBusy = true;
        try
        {
            var users = await _userRepository.GetAllAsync();
            Users = new ObservableCollection<UserInfo>(
                users.Where(u => string.IsNullOrWhiteSpace(SearchText)
                    || u.UserName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                    || u.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载用户列表失败");
            await _dialogService.ShowErrorAsync("加载用户列表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddUserAsync()
    {
        if (!EnsurePermission()) return;

        var window = _serviceProvider.GetRequiredService<Views.UserEditWindow>();
        if (window.DataContext is UserEditViewModel vm)
        {
            vm.InitializeForCreate();
        }

        window.ShowDialog();
        if (window.DataContext is UserEditViewModel editVm && editVm.IsSaved)
        {
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task EditUserAsync()
    {
        if (!EnsurePermission()) return;
        if (SelectedUser == null) return;

        var window = _serviceProvider.GetRequiredService<Views.UserEditWindow>();
        if (window.DataContext is UserEditViewModel vm)
        {
            vm.InitializeForEdit(SelectedUser);
        }

        window.ShowDialog();
        if (window.DataContext is UserEditViewModel editVm && editVm.IsSaved)
        {
            await LoadUsersAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync()
    {
        if (!EnsurePermission()) return;
        if (SelectedUser == null) return;

        if (SelectedUser.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowErrorAsync("默认管理员账号不能删除");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            $"确定要删除用户 {SelectedUser.DisplayName}({SelectedUser.UserName}) 吗？",
            "删除确认");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _userRepository.DeleteAsync(SelectedUser.Id);
            await LogAuditAsync(AuditActionType.Delete, SelectedUser.UserName, true, "删除用户");
            await LoadUsersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除用户失败");
            await _dialogService.ShowErrorAsync("删除用户失败：" + ex.Message);
            await LogAuditAsync(AuditActionType.Delete, SelectedUser.UserName, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ResetPasswordAsync()
    {
        if (!EnsurePermission()) return;
        if (SelectedUser == null) return;

        var confirm = await _dialogService.ShowConfirmAsync(
            $"确定要重置用户 {SelectedUser.DisplayName}({SelectedUser.UserName}) 的密码吗？\n重置后密码为：admin123",
            "重置密码确认");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            var hash = _passwordHasher.HashPassword("admin123");
            await _userRepository.UpdatePasswordAsync(SelectedUser.Id, hash);
            await _userRepository.UpdateAsync(new UserInfo
            {
                Id = SelectedUser.Id,
                UserName = SelectedUser.UserName,
                DisplayName = SelectedUser.DisplayName,
                IsEnabled = SelectedUser.IsEnabled,
                MustChangePassword = true
            });

            await LogAuditAsync(AuditActionType.Update, SelectedUser.UserName, true, "重置密码");
            await _dialogService.ShowAlertAsync("密码已重置为 admin123，首次登录需修改密码。", "重置成功");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "重置密码失败");
            await _dialogService.ShowErrorAsync("重置密码失败：" + ex.Message);
            await LogAuditAsync(AuditActionType.Update, SelectedUser.UserName, false, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsurePermission()
    {
        if (_identityService.HasPermission("user.manage")) return true;

        _dialogService.ShowErrorAsync("您没有用户管理权限").ConfigureAwait(false);
        return false;
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
