using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.PrismEvents;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.System.Services;
using AP.Plugin.Layout.Services;
using AP.Shared.PluginSDK.Navigation;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using Prism.Events;
using Prism.Navigation.Regions;

namespace AP.Plugin.Layout.ViewModels;

/// <summary>
/// 仪表盘（框架级首页）：只展示系统健康度、设备状态与运行概况，全部为真实数据——
/// 设备（设备注册表）、采集（点表+采集引擎计数）、系统资源（ISystemMonitorService）、
/// 数据库（DatabaseStatusService 探测）、最近事件（Prism 事件流）、快捷入口（导航贡献者）。
/// 不依赖任何具体业务 Tag；实时趋势/工艺曲线等项目内容归业务页面（LiveCharts2 能力保留给业务插件）。
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private const int MaxRecentEvents = 10;
    private const int MaxQuickLinks = 6;

    private readonly IIdentityService _identityService;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ITagTable _tagTable;
    private readonly ITagAcquisitionStatus _acquisitionStatus;
    private readonly ILatestTagValueStore _latestValueStore;
    private readonly IEventAggregator _eventAggregator;
    private readonly ISystemMonitorService _systemMonitor;
    private readonly DatabaseStatusService _databaseStatusService;
    private readonly IConfiguration _configuration;
    private readonly IRegionManager _regionManager;
    private SubscriptionToken? _deviceStateToken;
    private SubscriptionToken? _tagChangedToken;
    private SubscriptionToken? _scanToken;
    private readonly DispatcherTimer _timer;
    private readonly DateTime _startTime;
    private int _tickCount;

    // 欢迎区
    [ObservableProperty] private string _welcomeTitle = "";
    [ObservableProperty] private string _softwareName = "";
    [ObservableProperty] private string _currentTimeText = "";
    [ObservableProperty] private StatusBadge _healthBadge = new("", "none");

    // 六张统计卡：数字 / 后缀 / 状态徽标
    [ObservableProperty] private string _onlineDevices = "--";
    [ObservableProperty] private string _onlineDevicesSuffix = "";
    [ObservableProperty] private StatusBadge _onlineBadge = new("", "none");
    [ObservableProperty] private string _alarmCount = "--";
    [ObservableProperty] private StatusBadge _alarmBadge = new("", "none");
    [ObservableProperty] private string _acquisitionPoints = "--";
    [ObservableProperty] private StatusBadge _acquisitionBadge = new("", "none");
    [ObservableProperty] private string _commSuccessRate = "--";
    [ObservableProperty] private StatusBadge _commBadge = new("", "none");
    [ObservableProperty] private string _systemResourceText = "--";
    [ObservableProperty] private StatusBadge _systemResourceBadge = new("", "none");
    [ObservableProperty] private string _uptime = "--";
    [ObservableProperty] private string _uptimeSuffix = "";
    [ObservableProperty] private StatusBadge _uptimeBadge = new("", "none");

    [ObservableProperty] private ObservableCollection<RecentEventItem> _recentEvents = new();
    [ObservableProperty] private ObservableCollection<DeviceStatusItem> _deviceItems = new();
    [ObservableProperty] private ObservableCollection<ServiceStatusItem> _serviceItems = new();
    [ObservableProperty] private ObservableCollection<QuickLinkItem> _quickLinks = new();

    public DashboardViewModel(
        IIdentityService identityService,
        IDeviceRegistry deviceRegistry,
        ITagTable tagTable,
        ITagAcquisitionStatus acquisitionStatus,
        ILatestTagValueStore latestValueStore,
        IEventAggregator eventAggregator,
        ISystemMonitorService systemMonitor,
        DatabaseStatusService databaseStatusService,
        IConfiguration configuration,
        IRegionManager regionManager,
        IEnumerable<INavigationContributor> navigationContributors)
    {
        _identityService = identityService;
        _deviceRegistry = deviceRegistry;
        _tagTable = tagTable;
        _acquisitionStatus = acquisitionStatus;
        _latestValueStore = latestValueStore;
        _eventAggregator = eventAggregator;
        _systemMonitor = systemMonitor;
        _databaseStatusService = databaseStatusService;
        _configuration = configuration;
        _regionManager = regionManager;
        _startTime = DateTime.Now;

        _softwareName = _configuration["AppConfiguration:SoftwareName"] ?? "自动化监控系统";

        RefreshUser();
        RefreshCurrentTime();
        RefreshDevices();
        RefreshAlarms();
        RefreshAcquisitionPoints();
        RefreshCommSuccessRate();
        RefreshServiceStatus();
        RefreshHealth();
        RefreshUptime();
        BuildQuickLinks(navigationContributors);
        AddRecentEvent("系统启动完成", _startTime, RecentEventLevel.Success);
        SubscribeEvents();

        // 1s 统一 tick：时间每秒；设备/告警/采集/成功率/资源/健康每 2s；运行时间与数据库探测每 30s
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
        _timer.Start();

        _ = RefreshSystemResourcesAsync();
        _ = RefreshDatabaseStatusAsync();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _tickCount++;
        RefreshCurrentTime();

        if (_tickCount % 2 == 0)
        {
            RefreshDevices();
            RefreshAlarms();
            RefreshAcquisitionPoints();
            RefreshCommSuccessRate();
            RefreshEngineServiceStatus();
            RefreshHealth();
            _ = RefreshSystemResourcesAsync();
        }

        if (_tickCount % 30 == 0)
        {
            RefreshUptime();
            _ = RefreshDatabaseStatusAsync();
        }
    }

    // --- 快捷入口（复用导航贡献者，排除首页自身） ---

    private void BuildQuickLinks(IEnumerable<INavigationContributor> navigationContributors)
    {
        var defaultTarget = _configuration["AppConfiguration:DefaultNavigationTarget"];
        var securityEnabled = _configuration.GetValue<bool?>("Security:Enabled") ?? true;
        var allowedWhenSecurityDisabled = _configuration
            .GetSection("AppConfiguration:NavigationWhenSecurityDisabled")
            .Get<string[]>() ?? [];

        Func<NavigationMenuItem, bool>? visibilityFilter = null;
        if (!securityEnabled)
        {
            visibilityFilter = item => allowedWhenSecurityDisabled.Contains(item.NavigationTarget, StringComparer.OrdinalIgnoreCase);
        }

        var menuItems = NavigationMenuItemBuilder.Build(
            navigationContributors,
            _identityService.HasPermission,
            defaultTarget,
            visibilityFilter);

        QuickLinks = new ObservableCollection<QuickLinkItem>(menuItems
            .Where(m => !string.Equals(m.NavigationTarget, "DashboardView", StringComparison.OrdinalIgnoreCase))
            .Take(MaxQuickLinks)
            .Select(m => new QuickLinkItem(m.Label, m.IconKind,
                new RelayCommand(() => _regionManager.RequestNavigate(
                    AP.Shared.Utilities.Constants.GlobalConstants.RegionNames.ContentRegion,
                    m.NavigationTarget)))));
    }

    // --- 事件订阅 ---

    private void SubscribeEvents()
    {
        _deviceStateToken = _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Subscribe(e =>
            RunOnUi(() =>
            {
                RefreshDevices();
                RefreshAlarms();
                RefreshHealth();
                AddRecentEvent($"{e.Info.Name} {StateText(e.Transition.To)}", e.Transition.Timestamp,
                    StateLevel(e.Transition.To));
            }));

        _tagChangedToken = _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Subscribe(e =>
            RunOnUi(() => AddRecentEvent(
                e.Value.Quality == TagQuality.Good ? $"{e.Name} = {e.Value.Value}" : $"{e.Name} 质量异常",
                e.Value.Timestamp.LocalDateTime,
                e.Value.Quality == TagQuality.Good ? RecentEventLevel.Info : RecentEventLevel.Warning)));

        _scanToken = _eventAggregator.GetEvent<PrismScanCompletedEvent>().Subscribe(e =>
            RunOnUi(() => AddRecentEvent($"扫码完成：{e.Barcode}", e.Timestamp, RecentEventLevel.Info)));
    }

    // --- 各卡片刷新 ---

    private void RefreshDevices()
    {
        var devices = _deviceRegistry.Devices;
        var online = devices.Count(d => d.State == DeviceConnectionState.Connected);

        OnlineDevices = devices.Count == 0 ? "--" : online.ToString();
        OnlineDevicesSuffix = devices.Count == 0 ? "" : $"/{devices.Count} 台";

        if (devices.Count == 0)
        {
            OnlineBadge = new StatusBadge("无设备", "none");
        }
        else if (online == devices.Count)
        {
            OnlineBadge = new StatusBadge("全部在线", "ok");
        }
        else
        {
            OnlineBadge = new StatusBadge(online == 0 ? "全部离线" : $"{devices.Count - online} 台离线", "err");
        }

        DeviceItems = new ObservableCollection<DeviceStatusItem>(devices.Select(d =>
            new DeviceStatusItem(
                d.Info.Name,
                string.IsNullOrWhiteSpace(d.Info.DriverType) ? d.Info.Type.ToString() : d.Info.DriverType,
                new StatusBadge(StateText(d.State), StateBadgeLevel(d.State)))));
    }

    /// <summary>当前告警 = 离线/故障/重连中设备数 + Bad 质量点数（均为框架级信号，不依赖业务语义）。</summary>
    private void RefreshAlarms()
    {
        var devices = _deviceRegistry.Devices;
        var deviceAlarms = devices.Count(d => d.State is DeviceConnectionState.Disconnected
            or DeviceConnectionState.Faulted or DeviceConnectionState.Reconnecting);
        var badTags = _latestValueStore.Snapshot().Count(kv => kv.Value.Quality == TagQuality.Bad);
        var alarms = deviceAlarms + badTags;

        AlarmCount = alarms.ToString();
        AlarmBadge = alarms == 0
            ? new StatusBadge("无告警", "ok")
            : new StatusBadge($"{deviceAlarms} 设备 · {badTags} 点位", "err");
    }

    private void RefreshAcquisitionPoints()
    {
        AcquisitionPoints = _tagTable.Tags.Count.ToString();
        AcquisitionBadge = _acquisitionStatus.IsRunning
            ? new StatusBadge("采集中", "ok")
            : new StatusBadge("全部停止", "warn");
    }

    private void RefreshCommSuccessRate()
    {
        var total = _acquisitionStatus.TotalReads;
        var failed = _acquisitionStatus.FailedReads;

        if (total == 0)
        {
            CommSuccessRate = "--";
            CommBadge = new StatusBadge("暂无数据", "none");
            return;
        }

        CommSuccessRate = $"{(1.0 - (double)failed / total) * 100:0.0}";
        CommBadge = failed == 0
            ? new StatusBadge("全部成功", "ok")
            : new StatusBadge($"{failed} 次失败", "warn");
    }

    private async Task RefreshSystemResourcesAsync()
    {
        try
        {
            var metrics = await _systemMonitor.GetMetricsAsync();
            if (metrics.CpuUsage < 0)
            {
                SystemResourceText = "--";
                SystemResourceBadge = new StatusBadge("不可用", "none");
                return;
            }

            SystemResourceText = $"CPU {metrics.CpuUsage:0}% · 内存 {metrics.MemoryUsage:0}MB";
            SystemResourceBadge = metrics.CpuUsage switch
            {
                >= 90 => new StatusBadge("负载过高", "err"),
                >= 70 => new StatusBadge("负载偏高", "warn"),
                _ => new StatusBadge("正常", "ok"),
            };
        }
        catch
        {
            SystemResourceText = "--";
            SystemResourceBadge = new StatusBadge("不可用", "none");
        }
    }

    private void RefreshServiceStatus()
    {
        var auditEnabled = _configuration.GetValue<bool?>("Security:Audit:Enabled")
            ?? _configuration.GetValue<bool?>("Security:Enabled")
            ?? true;

        ServiceItems =
        [
            new ServiceStatusItem("数据库", new StatusBadge("探测中", "none")),
            new ServiceStatusItem("采集引擎", _acquisitionStatus.IsRunning
                ? new StatusBadge("运行中", "ok")
                : new StatusBadge("已停止", "warn")),
            new ServiceStatusItem("审计服务", auditEnabled
                ? new StatusBadge("已启用", "ok")
                : new StatusBadge("已关闭", "none")),
            new ServiceStatusItem("资源监控", new StatusBadge("正常", "ok")),
        ];
    }

    /// <summary>采集引擎服务行随 2s tick 刷新：引擎启动晚于 VM 构造，只初始化一次会定格在"已停止"。</summary>
    private void RefreshEngineServiceStatus()
    {
        if (ServiceItems.Count < 2)
        {
            RefreshServiceStatus();
            return;
        }

        ServiceItems[1] = new ServiceStatusItem("采集引擎", _acquisitionStatus.IsRunning
            ? new StatusBadge("运行中", "ok")
            : new StatusBadge("已停止", "warn"));
    }

    private async Task RefreshDatabaseStatusAsync()
    {
        var (text, level) = await _databaseStatusService.ProbeAsync();
        if (ServiceItems.Count == 0)
            RefreshServiceStatus();

        ServiceItems[0] = new ServiceStatusItem("数据库", new StatusBadge(text, level));
    }

    /// <summary>健康结论：全部设备在线 + 采集运行 + 无 Bad 质量点 → 正常；否则给出异常项数。</summary>
    private void RefreshHealth()
    {
        var issues = _deviceRegistry.Devices.Count(d => d.State != DeviceConnectionState.Connected
            && d.State != DeviceConnectionState.Disabled);
        if (_tagTable.Tags.Count > 0 && !_acquisitionStatus.IsRunning)
            issues++;
        issues += _latestValueStore.Snapshot().Count(kv => kv.Value.Quality == TagQuality.Bad);

        HealthBadge = issues == 0
            ? new StatusBadge("系统运行正常", "ok")
            : new StatusBadge($"存在 {issues} 项异常，请检查设备与采集状态", "warn");
    }

    private void AddRecentEvent(string title, DateTime timestamp, RecentEventLevel level)
    {
        RecentEvents.Insert(0, new RecentEventItem(title, timestamp, level));
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

    private static string StateBadgeLevel(DeviceConnectionState state) => state switch
    {
        DeviceConnectionState.Connected => "ok",
        DeviceConnectionState.Connecting or DeviceConnectionState.Reconnecting => "warn",
        DeviceConnectionState.Disabled => "none",
        _ => "err",
    };

    private static RecentEventLevel StateLevel(DeviceConnectionState state) => state switch
    {
        DeviceConnectionState.Connected => RecentEventLevel.Success,
        DeviceConnectionState.Connecting or DeviceConnectionState.Reconnecting => RecentEventLevel.Info,
        DeviceConnectionState.Disabled => RecentEventLevel.Warning,
        _ => RecentEventLevel.Error,
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

    private void RefreshUser()
    {
        var user = _identityService.CurrentUser;
        var displayName = "操作员";
        if (user != null && !string.Equals(user.UserName, "anonymous", StringComparison.OrdinalIgnoreCase))
        {
            // 免登录场景（AnonymousIdentityService）不展示英文匿名标识
            displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.UserName : user.DisplayName;
        }

        var greeting = DateTime.Now.Hour switch
        {
            < 6 => "夜深了",
            < 12 => "上午好",
            < 14 => "中午好",
            < 18 => "下午好",
            _ => "晚上好",
        };
        WelcomeTitle = $"{greeting}，{displayName}";
    }

    private void RefreshCurrentTime()
    {
        CurrentTimeText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss dddd");
    }

    private void RefreshUptime()
    {
        var elapsed = DateTime.Now - _startTime;
        if (elapsed.TotalHours >= 1)
        {
            Uptime = ((int)elapsed.TotalHours).ToString();
            UptimeSuffix = $" 小时 {elapsed.Minutes} 分钟";
        }
        else
        {
            Uptime = Math.Max(elapsed.Minutes, 0).ToString();
            UptimeSuffix = " 分钟";
        }

        UptimeBadge = elapsed.TotalMinutes < 5
            ? new StatusBadge("系统刚启动", "warn")
            : new StatusBadge("", "none");
    }

    public override void Destroy()
    {
        _timer.Stop();
        if (_deviceStateToken != null) _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Unsubscribe(_deviceStateToken);
        if (_tagChangedToken != null) _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Unsubscribe(_tagChangedToken);
        if (_scanToken != null) _eventAggregator.GetEvent<PrismScanCompletedEvent>().Unsubscribe(_scanToken);
        base.Destroy();
    }
}

/// <summary>状态徽标（色点 + 文本）。Level：ok/warn/err/none；Text 为空时界面隐藏。</summary>
public record StatusBadge(string Text, string Level);

public enum RecentEventLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public record RecentEventItem(string Title, DateTime Timestamp, RecentEventLevel Level)
{
    public string TimestampText => Timestamp.ToString("HH:mm:ss");

    public string LevelText => Level switch
    {
        RecentEventLevel.Success => "成功",
        RecentEventLevel.Warning => "警告",
        RecentEventLevel.Error => "严重",
        _ => "信息",
    };
}

/// <summary>设备状态总览行。</summary>
public record DeviceStatusItem(string Name, string TypeText, StatusBadge State);

/// <summary>系统服务状态行。</summary>
public record ServiceStatusItem(string Name, StatusBadge Status);

/// <summary>快捷入口（图标 + 文字 + 导航命令）。</summary>
public record QuickLinkItem(string Label, string IconKind, RelayCommand Command);
