using System.Windows;

namespace AP.Plugin.SystemSettings.Views;

/// <summary>
/// 系统配置对话框
/// </summary>
public partial class SettingsDialogWindow : Window
{
    public SettingsDialogWindow()
    {
        InitializeComponent();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
