using CommunityToolkit.Mvvm.ComponentModel;

namespace AP.Host.Desktop.ViewModels;

/// <summary>
/// 启动画面 ViewModel
/// </summary>
public partial class SplashViewModel : ObservableObject
{
    [ObservableProperty]
    private string _softwareName = "自动化监控系统";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _statusText = "正在启动...";
}
