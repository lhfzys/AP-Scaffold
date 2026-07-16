using System.Collections.ObjectModel;
using System.Windows.Threading;
using AP.Contracts.Security.Abstractions;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Prism.Navigation.Regions;

namespace AP.Plugin.Layout.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private readonly IIdentityService _identityService;
    private readonly IRegionManager _regionManager;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DateTime _startTime;

    [ObservableProperty] private string _displayName = "用户";
    [ObservableProperty] private string _greetingText = "";
    [ObservableProperty] private string _currentDate = "";
    [ObservableProperty] private string _activeUsers = "--";
    [ObservableProperty] private string _onlineDevices = "--";
    [ObservableProperty] private string _todayEvents = "--";
    [ObservableProperty] private string _uptime = "--";
    [ObservableProperty] private ObservableCollection<RecentEventItem> _recentEvents = new();

    public DashboardViewModel(IIdentityService identityService, IRegionManager regionManager)
    {
        _identityService = identityService;
        _regionManager = regionManager;
        _startTime = DateTime.Now;

        RefreshGreeting();
        RefreshUser();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += (_, _) => RefreshUptime();
        _uptimeTimer.Start();

        RefreshUptime();
        LoadPlaceholderData();
    }

    private void RefreshGreeting()
    {
        var hour = DateTime.Now.Hour;
        GreetingText = hour switch
        {
            < 6 => "夜深了，注意休息 🌙",
            < 12 => "早上好，今天也是高效的一天 ☀️",
            < 14 => "中午好，别忘了按时吃饭 🍜",
            < 18 => "下午好，继续加油 💪",
            _ => "晚上好，总结一下今天的成果 🌟"
        };

        CurrentDate = DateTime.Now.ToString("yyyy年M月d日 dddd");
    }

    private void RefreshUser()
    {
        var user = _identityService.CurrentUser;
        if (user != null)
        {
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        }
    }

    private void RefreshUptime()
    {
        var elapsed = DateTime.Now - _startTime;
        Uptime = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours} 小时 {elapsed.Minutes} 分钟"
            : $"{elapsed.Minutes} 分钟";
    }

    private void LoadPlaceholderData()
    {
        ActiveUsers = "5";
        OnlineDevices = "2";
        TodayEvents = "128";

        RecentEvents = new ObservableCollection<RecentEventItem>
        {
            new("系统启动完成", DateTime.Now.AddMinutes(-DateTime.Now.Minute).AddSeconds(-30)),
            new("PLC 连接成功", DateTime.Now.AddMinutes(-12)),
            new("用户 admin 登录", DateTime.Now.AddMinutes(-5)),
            new("配方「默认配方」已切换", DateTime.Now.AddMinutes(-2))
        };
    }

    [RelayCommand]
    private void Navigate(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;

        _regionManager.RequestNavigate(
            AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
            target);
    }

    public override void Destroy()
    {
        _uptimeTimer.Stop();
        base.Destroy();
    }
}

public record RecentEventItem(string Title, DateTime Timestamp)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss");
}