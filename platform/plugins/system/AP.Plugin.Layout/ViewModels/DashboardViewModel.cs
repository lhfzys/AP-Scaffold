using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.PrismEvents;
using AP.Contracts.Security.Abstractions;
using AP.Infra.Hardware.DeviceRuntime;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Events;

namespace AP.Plugin.Layout.ViewModels;

/// <summary>
/// 仪表盘：全部为真实数据——设备状态（设备注册表）、采集点（点表+采集引擎）、
/// Tag 变化计数与最近事件（Prism 事件流）。无任何占位数据。
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private const int MaxRecentEvents = 10;

    private readonly IIdentityService _identityService;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ITagTable _tagTable;
    private readonly TagAcquisitionEngine _acquisitionEngine;
    private readonly IEventAggregator _eventAggregator;
    private SubscriptionToken? _deviceStateToken;
    private SubscriptionToken? _tagChangedToken;
    private SubscriptionToken? _scanToken;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DateTime _startTime;
    private int _tagChangeCount;

    [ObservableProperty] private string _displayName = "用户";
    [ObservableProperty] private string _greetingText = "";
    [ObservableProperty] private string _currentDate = "";
    [ObservableProperty] private string _acquisitionPoints = "--";
    [ObservableProperty] private string _onlineDevices = "--";
    [ObservableProperty] private string _tagChanges = "0";
    [ObservableProperty] private string _uptime = "--";
    [ObservableProperty] private ObservableCollection<RecentEventItem> _recentEvents = new();

    public DashboardViewModel(
        IIdentityService identityService,
        IDeviceRegistry deviceRegistry,
        ITagTable tagTable,
        TagAcquisitionEngine acquisitionEngine,
        IEventAggregator eventAggregator)
    {
        _identityService = identityService;
        _deviceRegistry = deviceRegistry;
        _tagTable = tagTable;
        _acquisitionEngine = acquisitionEngine;
        _eventAggregator = eventAggregator;
        _startTime = DateTime.Now;

        RefreshGreeting();
        RefreshUser();
        RefreshDevices();
        RefreshAcquisitionPoints();
        AddRecentEvent("系统启动完成", _startTime);
        SubscribeEvents();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += (_, _) =>
        {
            RefreshUptime();
            RefreshDevices();
            RefreshAcquisitionPoints();
        };
        _uptimeTimer.Start();

        RefreshUptime();
    }

    private void SubscribeEvents()
    {
        _deviceStateToken = _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Subscribe(e =>
            RunOnUi(() =>
            {
                RefreshDevices();
                AddRecentEvent($"{e.Info.Name} {StateText(e.Transition.To)}", e.Transition.Timestamp);
            }));

        _tagChangedToken = _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Subscribe(e =>
            RunOnUi(() =>
            {
                TagChanges = (++_tagChangeCount).ToString();
                AddRecentEvent(
                    e.Value.Quality == TagQuality.Good ? $"{e.Name} = {e.Value.Value}" : $"{e.Name} 质量异常",
                    e.Value.Timestamp.LocalDateTime);
            }));

        _scanToken = _eventAggregator.GetEvent<PrismScanCompletedEvent>().Subscribe(e =>
            RunOnUi(() => AddRecentEvent($"扫码完成: {e.Barcode}", e.Timestamp)));
    }

    private void RefreshDevices()
    {
        var devices = _deviceRegistry.Devices;
        var online = devices.Count(d => d.State == DeviceConnectionState.Connected);
        OnlineDevices = $"{online}/{devices.Count}";
    }

    private void RefreshAcquisitionPoints()
    {
        AcquisitionPoints = $"{_tagTable.Tags.Count} 点·{(_acquisitionEngine.IsRunning ? "运行中" : "已停止")}";
    }

    private void AddRecentEvent(string title, DateTime timestamp)
    {
        RecentEvents.Insert(0, new RecentEventItem(title, timestamp));
        while (RecentEvents.Count > MaxRecentEvents)
            RecentEvents.RemoveAt(RecentEvents.Count - 1);
    }

    private static string StateText(DeviceConnectionState state) => state switch
    {
        DeviceConnectionState.Connected => "已连接",
        DeviceConnectionState.Connecting => "连接中",
        DeviceConnectionState.Reconnecting => "重连中",
        DeviceConnectionState.Disconnected => "已断开",
        DeviceConnectionState.Faulted => "故障",
        DeviceConnectionState.Disabled => "已停用",
        _ => state.ToString(),
    };

    /// <summary>
    /// 在 UI 线程执行事件处理。必须用 BeginInvoke（火忘排队）：
    /// 事件经由"驱动 Transitioned → MediatR.Publish 同步前缀 → Prism"在发布方线程同步触达本方法，
    /// 若用 Invoke 同步等待 UI，关闭流程中 UI 线程阻塞于 OnExit 等待关闭任务时会形成双向等待死锁
    /// （2026-07-26 优雅关闭卡死的真实根因）。
    /// </summary>
    private static void RunOnUi(Action action)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
            action();
        else
            Application.Current?.Dispatcher.BeginInvoke(action);
    }

    private void RefreshGreeting()
    {
        var hour = DateTime.Now.Hour;
        GreetingText = hour switch
        {
            < 6 => "夜深了，注意休息",
            < 12 => "早上好，今天也是高效的一天",
            < 14 => "中午好，别忘了按时吃饭",
            < 18 => "下午好，继续加油",
            _ => "晚上好，总结一下今天的成果"
        };

        CurrentDate = DateTime.Now.ToString("yyyy年M月d日 dddd");
    }

    private void RefreshUser()
    {
        var user = _identityService.CurrentUser;
        if (user == null || string.Equals(user.UserName, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            // 免登录场景（AnonymousIdentityService）：不展示英文匿名标识
            DisplayName = "操作员";
            return;
        }

        DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
    }

    private void RefreshUptime()
    {
        var elapsed = DateTime.Now - _startTime;
        Uptime = elapsed.TotalHours >= 1
            ? $"{(int)elapsed.TotalHours} 小时 {elapsed.Minutes} 分钟"
            : $"{elapsed.Minutes} 分钟";
    }

    public override void Destroy()
    {
        _uptimeTimer.Stop();
        if (_deviceStateToken != null) _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Unsubscribe(_deviceStateToken);
        if (_tagChangedToken != null) _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Unsubscribe(_tagChangedToken);
        if (_scanToken != null) _eventAggregator.GetEvent<PrismScanCompletedEvent>().Unsubscribe(_scanToken);
        base.Destroy();
    }
}

public record RecentEventItem(string Title, DateTime Timestamp)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss");
}
