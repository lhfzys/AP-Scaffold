#region

using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
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

    /// <summary>底部用户卡可见性：仅登录模式显示（免登录下 CurrentUser 恒为 anonymous，无展示价值）</summary>
    [ObservableProperty]
    private bool _isUserCardVisible;

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
        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        IsUserCardVisible = securityEnabled;
        var allowedWhenSecurityDisabled = configuration
            .GetSection("AppConfiguration:NavigationWhenSecurityDisabled")
            .Get<string[]>() ?? Array.Empty<string>();

        Func<NavigationMenuItem, bool>? visibilityFilter = null;
        if (!securityEnabled)
        {
            // 未启用安全模块时，只显示白名单中的菜单（默认回退到仪表盘）
            visibilityFilter = item => allowedWhenSecurityDisabled.Contains(item.NavigationTarget, StringComparer.OrdinalIgnoreCase);
        }

        var menuItems = NavigationMenuItemBuilder.Build(
            navigationContributors,
            identityService.HasPermission,
            defaultTarget,
            visibilityFilter);

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

        // 延迟到 UI 线程就绪后再设置默认选中项，确保 Region 已附加并可导航
        Application.Current.Dispatcher?.BeginInvoke(() =>
        {
            SelectedItem = NavigationItems.FirstOrDefault(i => i.IsSelected && i.IsVisible)
                           ?? NavigationItems.FirstOrDefault(i => i.IsVisible);
            SubscribeRegionNavigation();
        }, DispatcherPriority.Background);
    }

    /// <summary>同步选中状态时置位，避免回写 SelectedItem 再次触发导航。</summary>
    private bool _syncingSelection;

    /// <summary>
    /// 订阅内容区导航完成事件：从 Sidebar 之外发起的导航（如首页快捷入口）也能同步左侧选中态。
    /// </summary>
    private void SubscribeRegionNavigation()
    {
        try
        {
            var region = _regionManager.Regions[
                AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion];
            region.NavigationService.Navigated += OnRegionNavigated;
        }
        catch (KeyNotFoundException)
        {
            // Region 尚未注册时放弃同步：Sidebar 自身点击导航不受影响
        }
    }

    private void OnRegionNavigated(object? sender, RegionNavigationEventArgs e)
    {
        var target = e.NavigationContext.Uri.OriginalString.Split('?')[0];
        if (string.IsNullOrWhiteSpace(target)) return;

        Application.Current.Dispatcher?.BeginInvoke(() =>
        {
            var item = NavigationItems.FirstOrDefault(i =>
                i.IsVisible && string.Equals(i.NavigationTarget, target, StringComparison.OrdinalIgnoreCase));
            if (item == null || item == SelectedItem) return;

            _syncingSelection = true;
            SelectedItem = item;
            _syncingSelection = false;
        });
    }

    partial void OnSelectedItemChanged(NavigationItem? value)
    {
        if (value == null) return;

        foreach (var item in NavigationItems)
        {
            item.IsSelected = item == value;
        }

        if (string.IsNullOrEmpty(value.NavigationTarget)) return;

        // 由导航事件同步选中（快捷入口等外部导航）时不再重复发起导航
        if (_syncingSelection) return;

        // 二次校验权限，防止未授权导航
        var sourceItem = NavigationItems.FirstOrDefault(i =>
            i.NavigationTarget.Equals(value.NavigationTarget, StringComparison.OrdinalIgnoreCase));
        if (sourceItem != null && !sourceItem.IsVisible) return;

        _regionManager.RequestNavigate(
            AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
            value.NavigationTarget,
            navigationResult =>
            {
                if (!navigationResult.Success)
                {
                    System.Diagnostics.Debug.WriteLine($"导航到 {value.NavigationTarget} 失败");
                }
            });
    }

    private void OnNavigate(NavigationItem? item)
    {
        if (item == null) return;
        SelectedItem = item;
    }
}
