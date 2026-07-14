#region

using System.Windows;
using System.Windows.Threading;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Contracts.System.Services;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

#endregion

namespace AP.Plugin.Layout.ViewModels;

public partial class LayoutViewModel : ViewModelBase
{
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ILoginService _loginService;
    private readonly IServiceProvider _serviceProvider;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _companyName = "未配置";
    [ObservableProperty] private string _softwareName = "未配置";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentUserName = "未登录";
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _canLogout;

    public LayoutViewModel(
        IConfiguration configuration,
        ISettingsDialogService settingsDialogService,
        IIdentityService identityService,
        IAuditService auditService,
        ILoginService loginService,
        IServiceProvider serviceProvider)
    {
        _settingsDialogService = settingsDialogService;
        _identityService = identityService;
        _auditService = auditService;
        _loginService = loginService;
        _serviceProvider = serviceProvider;

        CompanyName = configuration["AppConfiguration:CompanyName"] ?? "Automation";
        SoftwareName = configuration["AppConfiguration:SoftwareName"] ?? "Platform";

        _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer.Start();

        CanLogout = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        RefreshCurrentUser();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _settingsDialogService.ShowSettingsDialog();
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        var userName = _identityService.CurrentUser?.UserName ?? "unknown";

        await _identityService.LogoutAsync();
        RefreshCurrentUser();

        await LogAuditAsync(AuditActionType.Logout, userName, true, "用户退出登录");

        var mainWindow = Application.Current.MainWindow;
        mainWindow.Hide();

        if (_loginService.ShowLoginDialog())
        {
            RefreshCurrentUser();
            mainWindow.Show();
            await LogAuditAsync(AuditActionType.Login, _identityService.CurrentUser?.UserName ?? userName, true, "重新登录");
        }
        else
        {
            Application.Current.Shutdown();
        }
    }

    [RelayCommand]
    private void ExitSystem()
    {
        Application.Current.Shutdown();
    }

    private void RefreshCurrentUser()
    {
        var user = _identityService.CurrentUser;
        if (user == null)
        {
            CurrentUserName = "未登录";
            IsAuthenticated = false;
            return;
        }

        CurrentUserName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : $"{user.DisplayName}({user.UserName})";
        IsAuthenticated = true;
    }

    private async Task LogAuditAsync(AuditActionType actionType, string userName, bool succeeded, string description)
    {
        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                UserName = userName,
                ActionType = actionType,
                ActionName = description,
                Succeeded = succeeded
            });
        }
        catch
        {
            // 审计记录失败不应影响主流程
        }
    }

    public override void Destroy()
    {
        _timer.Stop();
        base.Destroy();
    }
}
