using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.PrismEvents;
using AP.Contracts.Security.Abstractions;
using AP.Infra.Hardware.DeviceRuntime;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using Prism.Events;
using SkiaSharp;

namespace AP.Plugin.Layout.ViewModels;

/// <summary>
/// 仪表盘：全部为真实数据——设备状态（设备注册表）、采集点（点表+采集引擎）、
/// Tag 变化计数与最近事件（Prism 事件流）、实时趋势（内存环形缓冲，近 60 分钟）。
/// 无任何占位数据；24H/7D/30D 需历史持久化（停车场项），界面上禁用预留。
/// </summary>
public partial class DashboardViewModel : ViewModelBase
{
    private const int MaxRecentEvents = 10;
    private const int MaxTrendSeries = 4;
    private const int TrendCapacity = 1800; // 2s 采样 × 1800 = 60 分钟

    // 趋势序列配色（与主题语义色一致）
    private static readonly SKColor[] TrendPalette =
    [
        SKColor.Parse("#1E3A5F"), // Primary
        SKColor.Parse("#0891B2"), // Accent
        SKColor.Parse("#2563EB"), // Info
        SKColor.Parse("#D97706"), // Warning
    ];

    private readonly IIdentityService _identityService;
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly ITagTable _tagTable;
    private readonly TagAcquisitionEngine _acquisitionEngine;
    private readonly LatestTagValueStore _latestValueStore;
    private readonly IEventAggregator _eventAggregator;
    private SubscriptionToken? _deviceStateToken;
    private SubscriptionToken? _tagChangedToken;
    private SubscriptionToken? _scanToken;
    private readonly DispatcherTimer _uptimeTimer;
    private readonly DispatcherTimer _trendTimer;
    private readonly DateTime _startTime;
    private int _tagChangeCount;

    private readonly Dictionary<string, ObservableCollection<DateTimePoint>> _trendBuffers =
        new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private string _displayName = "用户";
    [ObservableProperty] private string _greetingText = "";
    [ObservableProperty] private string _currentDate = "";

    // 卡片：数字 / 后缀 / 徽标文本 / 徽标级别（ok/warn/err/none）
    [ObservableProperty] private string _acquisitionPoints = "--";
    [ObservableProperty] private string _acquisitionBadgeText = "";
    [ObservableProperty] private string _acquisitionBadgeLevel = "none";
    [ObservableProperty] private string _onlineDevices = "--";
    [ObservableProperty] private string _onlineDevicesSuffix = "";
    [ObservableProperty] private string _onlineBadgeText = "";
    [ObservableProperty] private string _onlineBadgeLevel = "none";
    [ObservableProperty] private string _tagChanges = "0";
    [ObservableProperty] private string _uptime = "--";
    [ObservableProperty] private string _uptimeSuffix = "";
    [ObservableProperty] private string _uptimeBadgeText = "";
    [ObservableProperty] private string _uptimeBadgeLevel = "none";

    [ObservableProperty] private bool _hasTrendSeries;
    [ObservableProperty] private ObservableCollection<RecentEventItem> _recentEvents = new();

    public DashboardViewModel(
        IIdentityService identityService,
        IDeviceRegistry deviceRegistry,
        ITagTable tagTable,
        TagAcquisitionEngine acquisitionEngine,
        LatestTagValueStore latestValueStore,
        IEventAggregator eventAggregator)
    {
        _identityService = identityService;
        _deviceRegistry = deviceRegistry;
        _tagTable = tagTable;
        _acquisitionEngine = acquisitionEngine;
        _latestValueStore = latestValueStore;
        _eventAggregator = eventAggregator;
        _startTime = DateTime.Now;

        InitializeTrend();

        RefreshGreeting();
        RefreshUser();
        RefreshDevices();
        RefreshAcquisitionPoints();
        AddRecentEvent("系统启动完成", _startTime, RecentEventLevel.Success);
        SubscribeEvents();

        _uptimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _uptimeTimer.Tick += (_, _) =>
        {
            RefreshUptime();
            RefreshDevices();
            RefreshAcquisitionPoints();
        };
        _uptimeTimer.Start();

        _trendTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _trendTimer.Tick += (_, _) => SampleTrend();
        _trendTimer.Start();

        RefreshUptime();
    }

    // --- 实时趋势（内存环形缓冲） ---

    public ObservableCollection<ISeries> TrendSeries { get; } = [];

    public Axis[] TrendXAxes { get; } =
    [
        new Axis
        {
            Labeler = value => new DateTime((long)value).ToString("HH:mm"),
            UnitWidth = TimeSpan.FromMinutes(10).Ticks,
            MinStep = TimeSpan.FromMinutes(10).Ticks,
        }
    ];

    public Axis[] TrendYAxes { get; } =
    [
        new Axis { MinLimit = 0 }
    ];

    /// <summary>按点表初始化趋势序列：数值型点（排除 Bool/String/ByteArray），最多 4 条。</summary>
    private void InitializeTrend()
    {
        var numericTags = _tagTable.Tags
            .Select(t => t.Definition)
            .Where(d => d.DataType is not (TagDataType.Bool or TagDataType.String or TagDataType.ByteArray))
            .Take(MaxTrendSeries)
            .ToList();

        var index = 0;
        foreach (var tag in numericTags)
        {
            var buffer = new ObservableCollection<DateTimePoint>();
            _trendBuffers[tag.Name] = buffer;

            var color = TrendPalette[index % TrendPalette.Length];
            TrendSeries.Add(new LineSeries<DateTimePoint>
            {
                Name = tag.Name,
                Values = buffer,
                Stroke = new SolidColorPaint(color) { StrokeThickness = 2 },
                Fill = index == 0 ? new SolidColorPaint(color.WithAlpha(40)) : null,
                GeometrySize = 0,
                LineSmoothness = 0.2,
            });
            index++;
        }

        HasTrendSeries = TrendSeries.Count > 0;
    }

    /// <summary>2s 采样：从最新值表取各序列当前值入环形缓冲（仅 Good 质量且可转数值）。</summary>
    private void SampleTrend()
    {
        var snapshot = _latestValueStore.Snapshot();
        var now = DateTime.Now;

        foreach (var (name, buffer) in _trendBuffers)
        {
            if (!snapshot.TryGetValue(name, out var value) || value.Quality != TagQuality.Good)
                continue;
            if (!TryToDouble(value.Value, out var number))
                continue;

            buffer.Add(new DateTimePoint(now, number));
            while (buffer.Count > TrendCapacity)
                buffer.RemoveAt(0);
        }
    }

    private static bool TryToDouble(object? value, out double number)
    {
        try
        {
            number = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            number = 0;
            return false;
        }
    }

    // --- 事件订阅 ---

    private void SubscribeEvents()
    {
        _deviceStateToken = _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Subscribe(e =>
            RunOnUi(() =>
            {
                RefreshDevices();
                AddRecentEvent($"{e.Info.Name} {StateText(e.Transition.To)}", e.Transition.Timestamp,
                    StateLevel(e.Transition.To));
            }));

        _tagChangedToken = _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Subscribe(e =>
            RunOnUi(() =>
            {
                TagChanges = (++_tagChangeCount).ToString();
                AddRecentEvent(
                    e.Value.Quality == TagQuality.Good ? $"{e.Name} = {e.Value.Value}" : $"{e.Name} 质量异常",
                    e.Value.Timestamp.LocalDateTime,
                    e.Value.Quality == TagQuality.Good ? RecentEventLevel.Info : RecentEventLevel.Warning);
            }));

        _scanToken = _eventAggregator.GetEvent<PrismScanCompletedEvent>().Subscribe(e =>
            RunOnUi(() => AddRecentEvent($"扫码完成：{e.Barcode}", e.Timestamp, RecentEventLevel.Info)));
    }

    private void RefreshDevices()
    {
        var devices = _deviceRegistry.Devices;
        var online = devices.Count(d => d.State == DeviceConnectionState.Connected);

        OnlineDevices = devices.Count == 0 ? "--" : online.ToString();
        OnlineDevicesSuffix = devices.Count == 0 ? "" : $"/{devices.Count} 台";

        if (devices.Count == 0)
        {
            OnlineBadgeText = "无设备";
            OnlineBadgeLevel = "none";
        }
        else if (online == devices.Count)
        {
            OnlineBadgeText = "全部在线";
            OnlineBadgeLevel = "ok";
        }
        else
        {
            OnlineBadgeText = online == 0 ? "全部离线" : $"{devices.Count - online} 台离线";
            OnlineBadgeLevel = "err";
        }
    }

    private void RefreshAcquisitionPoints()
    {
        AcquisitionPoints = _tagTable.Tags.Count.ToString();
        AcquisitionBadgeText = _acquisitionEngine.IsRunning ? "采集中" : "全部停止";
        AcquisitionBadgeLevel = _acquisitionEngine.IsRunning ? "ok" : "warn";
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

        if (elapsed.TotalMinutes < 5)
        {
            UptimeBadgeText = "系统刚启动";
            UptimeBadgeLevel = "warn";
        }
        else
        {
            UptimeBadgeText = "";
            UptimeBadgeLevel = "none";
        }
    }

    public override void Destroy()
    {
        _uptimeTimer.Stop();
        _trendTimer.Stop();
        if (_deviceStateToken != null) _eventAggregator.GetEvent<PrismDeviceStateChangedEvent>().Unsubscribe(_deviceStateToken);
        if (_tagChangedToken != null) _eventAggregator.GetEvent<PrismTagValueChangedEvent>().Unsubscribe(_tagChangedToken);
        if (_scanToken != null) _eventAggregator.GetEvent<PrismScanCompletedEvent>().Unsubscribe(_scanToken);
        base.Destroy();
    }
}

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
