using AP.Contracts.Core.Events;
using AP.Host.Desktop.Configuration;
using AP.Shared.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace AP.Host.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ICustomDialogService _dialogService;
    private readonly DispatcherTimer _timer;
    [ObservableProperty] private bool _isLoading = true;

    private bool _canClose = false;

    // 头部显示的信息
    [ObservableProperty] private string _companyName = "气密检测监控系统"; // 公司名
    [ObservableProperty] private string _softwareName = "气密检测监控系统"; // 软件名
    [ObservableProperty] private string _windowTitle;
    [ObservableProperty] private string _currentTime; // 实时时间
    public IRelayCommand<CancelEventArgs> OnClosingCommand { get; }

    public MainWindowViewModel(IEventAggregator eventAggregator, IConfiguration configuration,
        ICustomDialogService dialogService)
    {
        _dialogService = dialogService;

        var appConfig = configuration.GetSection(AppConfigurationOptions.SectionName).Get<AppConfigurationOptions>();
        if (appConfig != null)
        {
            CompanyName = appConfig.CompanyName;
            SoftwareName = appConfig.SoftwareName;
        }

        WindowTitle = $"{CompanyName} - {SoftwareName}";

        eventAggregator.GetEvent<AppInitializedEvent>().Subscribe(OnInitialized, ThreadOption.UIThread);

        _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer.Start();

        OnClosingCommand = new RelayCommand<CancelEventArgs>(OnClosing);
    }

    private void OnInitialized()
    {
        IsLoading = false;
    }

    // [RelayCommand]
    private void OnClosing(CancelEventArgs e)
    {
        if (_canClose) return;
        e.Cancel = true;
        Application.Current.Dispatcher.InvokeAsync(async () => { await ExitSystem(); });
    }

    [RelayCommand]
    private async Task ExitSystem()
    {
        if (_canClose) return;
        var confirm = await _dialogService.ShowConfirmAsync("确定要退出自动化控制平台吗？\n退出后将停止所有数据采集。", "系统退出确认");

        if (confirm)
        {
            _canClose = true;
            Application.Current.Shutdown();
        }
    }
}