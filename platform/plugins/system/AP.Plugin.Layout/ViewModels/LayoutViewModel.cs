#region

using System.Windows;
using System.Windows.Threading;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;
using AP.Contracts.System.Services;

#endregion

namespace AP.Plugin.Layout.ViewModels;

public partial class LayoutViewModel : ViewModelBase
{
    private readonly ISettingsDialogService _settingsDialogService;
    private readonly DispatcherTimer _timer;

    [ObservableProperty] private string _companyName = "未配置";
    [ObservableProperty] private string _softwareName = "未配置";
    [ObservableProperty] private string _currentTime = "";

    public LayoutViewModel(IConfiguration configuration, ISettingsDialogService settingsDialogService)
    {
        _settingsDialogService = settingsDialogService;
        CompanyName = configuration["AppConfiguration:CompanyName"] ?? "Automation";
        SoftwareName = configuration["AppConfiguration:SoftwareName"] ?? "Platform";

        _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer.Start();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        _settingsDialogService.ShowSettingsDialog();
    }

    [RelayCommand]
    private void ExitSystem()
    {
        Application.Current.Shutdown();
    }

    public override void Destroy()
    {
        _timer.Stop();
        base.Destroy();
    }
}