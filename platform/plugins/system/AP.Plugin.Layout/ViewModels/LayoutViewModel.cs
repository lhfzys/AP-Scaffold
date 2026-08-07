#region

using System.Windows;
using System.Windows.Threading;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.PrismEvents;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Contracts.System.Services;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Prism.Events;

#endregion

namespace AP.Plugin.Layout.ViewModels;

public partial class LayoutViewModel : ViewModelBase
{
    private readonly IIdentityService _identityService;
    private readonly IAuditService _auditService;
    private readonly ILoginService _loginService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IEventAggregator _eventAggregator;
    private readonly Services.DatabaseStatusService _databaseStatusService;
    private readonly DispatcherTimer _timer;
    private SubscriptionToken? _deviceStateToken;

    [ObservableProperty] private string _companyName = "未配置";
    [ObservableProperty] private string _softwareName = "未配置";
    [ObservableProperty] private string _currentTime = "";
    [ObservableProperty] private string _currentUserName = "未登录";
    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private bool _canLogout;
    [ObservableProperty] private string _deviceStatusText = "设备 --/--";
    [ObservableProperty] private string _deviceStatusLevel = "none";
    [ObservableProperty] private string _systemMetricsText = "CPU --% · 内存 --MB";
    [ObservableProperty] private string _databaseStatusText = "数据库 --";
    [ObservableProperty] private string _versionText = "";

    public LayoutViewModel(
        IConfiguration configuration,
        IIdentityService identityService,
        IAuditService auditService,
        ILoginService loginService,
        IServiceProvider serviceProvider,
        IDeviceRegistry deviceRegistry,
        IEventAggregator eventAggregator,
        Services.DatabaseStatusService databaseStatusService)
    {
        _identityService = identityService;
        _auditService = auditService;
        _loginService = loginService;
        _serviceProvider = serviceProvider;
        _deviceRegistry = deviceRegistry;
        _eventAggregator = eventAggregator;
        _databaseStatusService = databaseStatusService;

        CompanyName = configuration["AppConfiguration:CompanyName"] ?? "Automation";
        SoftwareName = configuration["AppConfiguration:SoftwareName"] ?? "Platform";
        VersionText = $"v{GetInformationalVersion()}";

        _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTimerTick;
        _timer.Start();

        RefreshDeviceStatus();
        _deviceStateToken = _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>()
            .Subscribe(_ => RunOnUi(RefreshDeviceStatus));

        CanLogout = configuration.GetValue<bool?>("Security:Enabled") ?? true;
        RefreshCurrentUser();
    }

    private int _tickCount;

    private void OnTimerTick(object? sender, EventArgs e)
    {
        CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        // CPU/内存每 2 秒、数据库连通每 30 秒
        _tickCount++;
        if (_tickCount % 2 == 0) _ = RefreshSystemMetricsAsync();
        if (_tickCount % 30 == 0) _ = RefreshDatabaseStatusAsync();
    }

    private async Task RefreshSystemMetricsAsync()
    {
        try
        {
            var monitor = _serviceProvider.GetService<ISystemMonitorService>();
            if (monitor == null) return;

            var metrics = await monitor.GetMetricsAsync();
            SystemMetricsText = metrics.CpuUsage < 0
                ? "CPU --% · 内存 --MB"
                : $"CPU {metrics.CpuUsage:0}% · 内存 {metrics.MemoryUsage:0}MB";
        }
        catch
        {
            // 性能计数器不可用（权限/精简系统）时保持默认文本，不影响状态栏
        }
    }

    private async Task RefreshDatabaseStatusAsync()
    {
        var (text, _) = await _databaseStatusService.ProbeAsync();
        DatabaseStatusText = $"数据库 {text}";
    }

    private static string GetInformationalVersion()
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly();
        if (assembly == null) return "1.0.0";

        var info = System.Reflection.CustomAttributeExtensions
            .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(assembly)
            ?.InformationalVersion;
        // InformationalVersion 可能带 +commit 后缀，只取版本部分
        return info?.Split('+')[0] ?? "1.0.0";
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var regionManager = _serviceProvider.GetRequiredService<IRegionManager>();
        regionManager.RequestNavigate(
            AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
            "SettingsShellView");
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

        CurrentUserName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : $"{user.DisplayName}（{user.UserName}）";
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

    private void RefreshDeviceStatus()
    {
        var devices = _deviceRegistry.Devices;
        var online = devices.Count(d => d.State == DeviceConnectionState.Connected);

        DeviceStatusText = devices.Count == 0
            ? "无已注册设备"
            : $"设备在线 {online}/{devices.Count}";
        DeviceStatusLevel = devices.Count == 0 ? "none"
            : online == 0 ? "error"
            : online < devices.Count ? "warn"
            : "ok";
    }

    /// <summary>
    /// 在 UI 线程执行事件处理。必须用 BeginInvoke（火忘排队）：
    /// 事件经"驱动 Transitioned → MediatR.Publish 同步前缀 → Prism"在发布方线程同步触达，
    /// Invoke 同步等待 UI 会在关闭流程中形成双向等待死锁（2026-07-26 踩坑）。
    /// </summary>
    private static void RunOnUi(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
            action();
        else
            Application.Current?.Dispatcher.BeginInvoke(action);
    }

    public override void Destroy()
    {
        _timer.Stop();
        if (_deviceStateToken != null) _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Unsubscribe(_deviceStateToken);
        base.Destroy();
    }
}
