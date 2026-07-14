using AP.Contracts.System.Services;
using AP.Plugin.SystemSettings.ViewModels;
using AP.Plugin.SystemSettings.Views;
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

        // 创建配置外壳视图与 ViewModel，并建立绑定
        var viewModel = _serviceProvider.GetRequiredService<SettingsShellViewModel>();
        var settingsView = _serviceProvider.GetRequiredService<SettingsShellView>();
        settingsView.DataContext = viewModel;
        dialog.SettingsContentHost.Content = settingsView;

        // 保存/取消后关闭对话框，并在窗口关闭时取消事件订阅以避免内存泄漏
        EventHandler? closeHandler = null;
        closeHandler = (s, e) => dialog.Close();
        viewModel.RequestClose += closeHandler;
        dialog.Closed += (s, e) => viewModel.RequestClose -= closeHandler;

        dialog.ShowDialog();
    }
}
