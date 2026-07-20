#region

using System.Collections.ObjectModel;
using AP.Contracts.Security.Abstractions;
using AP.Plugin.Layout.Models;
using AP.Shared.PluginSDK.Navigation;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    private string _currentUserName = string.Empty;

    [ObservableProperty]
    private string _currentUserRole = string.Empty;

    public SidebarViewModel(
        IRegionManager regionManager,
        IIdentityService identityService,
        IConfiguration configuration,
        IEnumerable<INavigationContributor> navigationContributors)
    {
        _regionManager = regionManager;
        _identityService = identityService;

        var currentUser = _identityService.CurrentUser;
        CurrentUserName = currentUser?.DisplayName ?? currentUser?.UserName ?? "未登录";
        CurrentUserRole = currentUser?.Roles?.FirstOrDefault() ?? "—";

        var defaultTarget = configuration["AppConfiguration:DefaultNavigationTarget"];
        var menuItems = NavigationMenuItemBuilder.Build(navigationContributors, identityService.HasPermission, defaultTarget);

        NavigationItems = new ObservableCollection<NavigationItem>(
            menuItems.Select(item => new NavigationItem
            {
                IconKind = item.IconKind,
                Label = item.Label,
                NavigationTarget = item.NavigationTarget,
                IsVisible = string.IsNullOrWhiteSpace(item.Permission)
                    || identityService.HasPermission(item.Permission),
                IsSelected = item.IsDefault
            }));

        foreach (var item in NavigationItems)
        {
            item.Command = new RelayCommand<NavigationItem?>(OnNavigate);
        }

        // 默认选中首个可见的默认项（通常是仪表盘），并触发导航
        SelectedItem = NavigationItems.FirstOrDefault(i => i.IsSelected && i.IsVisible)
                       ?? NavigationItems.FirstOrDefault(i => i.IsVisible);
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value == null) return;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        if (string.IsNullOrEmpty(value.NavigationTarget)) return;

        // 二次校验权限，防止未授权导航
        var sourceItem = NavigationItems.FirstOrDefault(i =>
            i.NavigationTarget.Equals(value.NavigationTarget, StringComparison.OrdinalIgnoreCase));
        if (sourceItem != null && !sourceItem.IsVisible) return;

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
