using AP.Contracts.System.Services;
using AP.Plugin.SystemSettings.Views;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.SystemSettings.Services;

/// <summary>
/// 系统配置对话框服务实现
/// </summary>
public class SettingsDialogService : ISettingsDialogService
{
    private readonly IServiceProvider _serviceProvider;

    public SettingsDialogService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public void ShowSettingsDialog()
    {
        var owner = System.Windows.Application.Current?.MainWindow;
        var dialog = new SettingsDialogWindow
        {
            Owner = owner
        };

        // 将 SettingsShellView 作为内容注入
        var settingsView = _serviceProvider.GetRequiredService<SettingsShellView>();
        dialog.SettingsContentHost.Content = settingsView;

        dialog.ShowDialog();
    }
}
