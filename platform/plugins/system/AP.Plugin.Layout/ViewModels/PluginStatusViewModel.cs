using System.Collections.ObjectModel;
using AP.Core.Lifecycle;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AP.Plugin.Layout.ViewModels;

/// <summary>插件状态页：展示生命周期管理器中的插件状态快照。</summary>
public partial class PluginStatusViewModel : ViewModelBase
{
    private readonly PluginLifecycleManager _lifecycleManager;

    [ObservableProperty]
    private ObservableCollection<PluginStatusInfo> _plugins = new();

    public PluginStatusViewModel(PluginLifecycleManager lifecycleManager)
    {
        _lifecycleManager = lifecycleManager;
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        Plugins = new ObservableCollection<PluginStatusInfo>(_lifecycleManager.GetPluginStatuses());
    }
}
