using System.Windows.Controls;
using AP.Plugin.SystemSettings.ViewModels;

namespace AP.Plugin.SystemSettings.Views;

/// <summary>
/// 配置中心外壳视图
/// </summary>
public partial class SettingsShellView : UserControl
{
    public SettingsShellView(SettingsShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
