#region

using AP.Contracts.Security.Abstractions;
using AP.Contracts.System.Services;
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
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IIdentityService _identityService;

    [ObservableProperty]
    private bool _canManageUsers;

    public SidebarViewModel(
        IRegionManager regionManager,
        ISettingsDialogService settingsDialogService,
        IIdentityService identityService,
        IConfiguration configuration)
    {
        _regionManager = regionManager;
        _settingsDialogService = settingsDialogService;
        _identityService = identityService;

        var securityEnabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        CanManageUsers = securityEnabled && _identityService.HasPermission("user.manage");
    }

    [RelayCommand]
    private void OpenUserManagement()
    {
        if (!_identityService.HasPermission("user.manage"))
            return;

        _regionManager.RequestNavigate(
            AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
            "UserListView");
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _settingsDialogService.ShowSettingsDialog();
    }
}
