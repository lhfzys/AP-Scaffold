using System.Collections.ObjectModel;
using System.Windows;
using AP.Contracts.Hardware.Services;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AP.Plugin.SystemSettings.ViewModels;

/// <summary>PLC 通讯日志页：显示最近 200 条应用层请求/响应。</summary>
public partial class PlcDiagnosticViewModel : ViewModelBase
{
    private readonly IPlcDiagnosticLog _log;

    [ObservableProperty]
    private ObservableCollection<PlcDiagnosticEntry> _entries = new();

    [ObservableProperty]
    private bool _showOnlyFailures;

    [ObservableProperty]
    private string _statusText = string.Empty;

    public PlcDiagnosticViewModel(IPlcDiagnosticLog log)
    {
        _log = log;
        Title = "通讯日志";
        _log.Changed += OnLogChanged;
        Refresh();
    }

    partial void OnShowOnlyFailuresChanged(bool value) => Refresh();

    [RelayCommand]
    private void Refresh()
    {
        var entries = _log.GetRecent(200);
        if (ShowOnlyFailures)
            entries = entries.Where(e => !e.Success).ToList();

        Entries = new ObservableCollection<PlcDiagnosticEntry>(entries);
        StatusText = $"显示 {Entries.Count} 条（内存保留最近 500 条）";
    }

    [RelayCommand]
    private void Clear()
    {
        _log.Clear();
        Refresh();
    }

    private void OnLogChanged(object? sender, EventArgs e)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
            dispatcher.Invoke(Refresh);
        else
            Refresh();
    }

    public override void Destroy()
    {
        _log.Changed -= OnLogChanged;
        base.Destroy();
    }
}
