using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MaterialDesignThemes.Wpf;

namespace AP.Plugin.Layout.Models;

/// <summary>
/// 侧边栏导航项
/// </summary>
public partial class NavigationItem : ObservableObject
{
    [ObservableProperty]
    private PackIconKind _iconKind;

    [ObservableProperty]
    private string _label = string.Empty;

    [ObservableProperty]
    private string _navigationTarget = string.Empty;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private IRelayCommand? _command;
}
