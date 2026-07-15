#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Plugin.Layout.Models;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.Configuration;
using Prism.Navigation.Regions;

#endregion

namespace AP.Plugin.Layout.ViewModels;

/// <summary>
/// 左侧边栏导航 ViewModel
/// </summary>
public partial class SidebarViewModel : ViewModelBase
{
    private readonly IRegionManager _regionManager;
    private readonly IIdentityService _identityService;

    [ObservableProperty]
    private ObservableCollection<NavigationItem> _navigationItems = new();

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    public SidebarViewModel(
        IRegionManager regionManager,
        IIdentityService identityService,
        IConfiguration configuration)
    {
        _regionManager = regionManager;
        _identityService = identityService;

        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        var canManageUsers = securityEnabled && _identityService.HasPermission("user.manage");
        var canManageRoles = securityEnabled && _identityService.HasPermission("role.manage");
        var canViewAudit = securityEnabled && _identityService.HasPermission("audit.view");
        var canViewRecipe = securityEnabled && _identityService.HasPermission("recipe.view");

        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new()
            {
                IconKind = PackIconKind.Cog,
                Label = "系统配置",
                NavigationTarget = "SettingsShellView",
                IsSelected = true
            },
            new()
            {
                IconKind = PackIconKind.FlaskOutline,
                Label = "配方管理",
                NavigationTarget = "RecipeListView",
                IsVisible = canViewRecipe
            },
            new()
            {
                IconKind = PackIconKind.AccountMultiple,
                Label = "用户管理",
                NavigationTarget = "UserListView",
                IsVisible = canManageUsers
            },
            new()
            {
                IconKind = PackIconKind.ShieldAccount,
                Label = "角色管理",
                NavigationTarget = "RoleListView",
                IsVisible = canManageRoles
            },
            new()
            {
                IconKind = PackIconKind.ClipboardTextClock,
                Label = "审计日志",
                NavigationTarget = "AuditLogListView",
                IsVisible = canViewAudit
            }
        };

        foreach (var item in NavigationItems)
        {
            item.Command = new RelayCommand<NavigationItem?>(OnNavigate);
        }
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value == null) return;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        if (string.IsNullOrEmpty(value.NavigationTarget)) return;
        if (value.NavigationTarget == "UserListView" && !_identityService.HasPermission("user.manage"))
            return;
        if (value.NavigationTarget == "RoleListView" && !_identityService.HasPermission("role.manage"))
            return;
        if (value.NavigationTarget == "AuditLogListView" && !_identityService.HasPermission("audit.view"))
            return;
        if (value.NavigationTarget == "RecipeListView" && !_identityService.HasPermission("recipe.view"))
            return;

        _regionManager.RequestNavigate(
            AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
            value.NavigationTarget);
    }

    private void OnNavigate(NavigationItem? item)
    {
        if (item == null) return;
        SelectedItem = item;
    }
}
