#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Shared.UI.Base;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

#endregion

namespace AP.Plugin.RoleManagement.ViewModels;

/// <summary>
/// 角色编辑窗口 ViewModel
/// </summary>
public partial class RoleEditViewModel : ViewModelBase
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ICustomDialogService _dialogService;
    private readonly ILogger<RoleEditViewModel> _logger;

    [ObservableProperty]
    private string _title = "新增角色";

    [ObservableProperty]
    private long _roleId;

    [ObservableProperty]
    private string _roleName = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PermissionGroupItem> _availablePermissions = new();

    public bool IsSaved { get; private set; }

    private bool _isEdit;

    public RoleEditViewModel(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IIdentityService identityService,
        IAuditService auditService,
        ICustomDialogService dialogService,
        ILogger<RoleEditViewModel> logger)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _identityService = identityService;
        _auditService = auditService;
        _dialogService = dialogService;
        _logger = logger;
    }

    public async Task InitializeForCreateAsync()
    {
        IsSaved = false;
        _isEdit = false;
        Title = "新增角色";
        RoleId = 0;
        RoleName = string.Empty;
        Description = string.Empty;
        await LoadPermissionsAsync(Array.Empty<string>());
    }

    public async Task InitializeForEditAsync(RoleInfo role)
    {
        IsSaved = false;
        _isEdit = true;
        Title = "编辑角色";
        RoleId = role.Id;
        RoleName = role.Name;
        Description = role.Description;
        await LoadPermissionsAsync(role.Permissions);
    }

    private async Task LoadPermissionsAsync(IEnumerable<string> selectedCodes)
    {
        var selectedSet = new HashSet<string>(selectedCodes, StringComparer.OrdinalIgnoreCase);
        var permissions = await _permissionRepository.GetAllAsync();

        var groups = permissions
            .Select(p => new PermissionItem
            {
                Code = p.Code,
                Name = string.IsNullOrWhiteSpace(p.Name) || p.Name == p.Code ? GetFriendlyName(p.Code) : p.Name,
                IsSelected = selectedSet.Contains(p.Code)
            })
            .GroupBy(p => GetGroupName(p.Code))
            .Select(g => new PermissionGroupItem
            {
                GroupName = g.Key,
                Permissions = new ObservableCollection<PermissionItem>(g.OrderBy(p => p.Name))
            })
            .OrderBy(g => g.GroupName);

        AvailablePermissions = new ObservableCollection<PermissionGroupItem>(groups);
    }

    private static string GetFriendlyName(string code)
    {
        return code switch
        {
            "system.view" => "系统查看",
            "system.settings" => "系统设置",
            "recipe.view" => "配方查看",
            "recipe.edit" => "配方编辑",
            "recipe.switch" => "配方切换",
            "report.view" => "报表查看",
            "report.export" => "报表导出",
            "user.manage" => "用户管理",
            "role.manage" => "角色管理",
            "audit.view" => "审计日志",
            "device.config" => "设备参数配置",
            "test.start" => "启动检测",
            _ => code
        };
    }

    private static string GetGroupName(string code)
    {
        var prefix = code.Split('.')[0];
        return prefix switch
        {
            "system" => "系统",
            "recipe" => "配方",
            "report" => "报表",
            "user" => "用户",
            "role" => "角色",
            "device" => "设备",
            "test" => "检测",
            "audit" => "审计",
            _ => prefix
        };
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(RoleName))
        {
            await _dialogService.ShowErrorAsync("角色名称不能为空");
            return;
        }

        BusyText = "正在保存角色...";
        IsBusy = true;
        try
        {
            var roleInfo = new RoleInfo
            {
                Id = RoleId,
                Name = RoleName.Trim(),
                Description = Description.Trim(),
                Permissions = AvailablePermissions
                    .SelectMany(g => g.Permissions)
                    .Where(p => p.IsSelected)
                    .Select(p => p.Code)
                    .ToList()
            };

            if (_isEdit)
            {
                await _roleRepository.UpdateAsync(roleInfo);
                await LogAuditAsync(AuditActionType.Update, roleInfo.Name, true, "编辑角色");
            }
            else
            {
                var existing = await _roleRepository.GetByNameAsync(roleInfo.Name);
                if (existing != null)
                {
                    await _dialogService.ShowErrorAsync("角色名称已存在");
                    return;
                }

                await _roleRepository.CreateAsync(roleInfo);
                await LogAuditAsync(AuditActionType.Create, roleInfo.Name, true, "新增角色");
            }

            IsSaved = true;
            OnRequestClose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存角色失败");
            await _dialogService.ShowErrorAsync("保存角色失败：" + ex.Message);
            await LogAuditAsync(_isEdit ? AuditActionType.Update : AuditActionType.Create, RoleName, false, ex.Message);
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

/// <summary>
/// 权限选择项
/// </summary>
public partial class PermissionItem : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>
/// 权限分组项
/// </summary>
public partial class PermissionGroupItem : ObservableObject
{
    [ObservableProperty]
    private string _groupName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<PermissionItem> _permissions = new();
}
