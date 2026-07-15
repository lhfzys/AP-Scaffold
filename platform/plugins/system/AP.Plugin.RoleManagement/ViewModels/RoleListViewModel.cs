#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.RoleManagement.ViewModels;

/// <summary>
/// 角色列表 ViewModel
/// </summary>
public partial class RoleListViewModel : ViewModelBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RoleListViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<RoleInfo> _roles = new();

    [ObservableProperty]
    private RoleInfo? _selectedRole;

    [ObservableProperty]
    private string _searchText = string.Empty;

    public RoleListViewModel(
        IRoleRepository roleRepository,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        IServiceProvider serviceProvider,
        ILogger<RoleListViewModel> logger)
    {
        _roleRepository = roleRepository;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await LoadRolesAsync();
    }

    partial void OnSearchTextChanged(string value)
    {
        _ = LoadRolesAsync();
    }

    [RelayCommand]
    private async Task LoadRolesAsync()
    {
        IsBusy = true;
        try
        {
            var allRoles = await _roleRepository.GetAllAsync();
            var filtered = allRoles.Where(r =>
                string.IsNullOrWhiteSpace(SearchText) ||
                r.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                r.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));

            Roles = new ObservableCollection<RoleInfo>(filtered);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载角色列表失败");
            await _dialogService.ShowErrorAsync("加载角色列表失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AddRoleAsync()
    {
        if (!EnsurePermission()) return;

        var window = _serviceProvider.GetRequiredService<Views.RoleEditWindow>();
        if (window.DataContext is RoleEditViewModel vm)
        {
            await vm.InitializeForCreateAsync();
        }

        window.ShowDialog();
        if (window.DataContext is RoleEditViewModel editVm && editVm.IsSaved)
        {
            await LoadRolesAsync();
        }
    }

    [RelayCommand]
    private async Task EditRoleAsync()
    {
        if (!EnsurePermission()) return;
        if (SelectedRole == null) return;

        var window = _serviceProvider.GetRequiredService<Views.RoleEditWindow>();
        if (window.DataContext is RoleEditViewModel vm)
        {
            await vm.InitializeForEditAsync(SelectedRole);
        }

        window.ShowDialog();
        if (window.DataContext is RoleEditViewModel editVm && editVm.IsSaved)
        {
            await LoadRolesAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteRoleAsync()
    {
        if (!EnsurePermission()) return;
        if (SelectedRole == null) return;

        if (SelectedRole.Name.Equals("Administrator", StringComparison.OrdinalIgnoreCase))
        {
            await _dialogService.ShowErrorAsync("默认管理员角色不能删除");
            return;
        }

        var confirm = await _dialogService.ShowConfirmAsync(
            $"确定要删除角色 {SelectedRole.Name} 吗？",
            "删除确认");

        if (!confirm) return;

        IsBusy = true;
        try
        {
            await _roleRepository.DeleteAsync(SelectedRole.Id);
            await LogAuditAsync(AuditActionType.Delete, SelectedRole.Name, true, "删除角色");
            await LoadRolesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "删除角色失败");
            await _dialogService.ShowErrorAsync("删除角色失败：" + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool EnsurePermission()
    {
        if (_identityService.HasPermission("role.manage")) return true;

        _dialogService.ShowErrorAsync("您没有角色管理权限").ConfigureAwait(false);
        return false;
    }

    private async Task LogAuditAsync(AuditActionType actionType, string roleName, bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = _identityService.CurrentUser?.UserName ?? "unknown",
                ActionType = actionType,
                ActionName = description,
                TargetId = roleName,
                Succeeded = succeeded
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录审计日志失败");
        }
    }
}
