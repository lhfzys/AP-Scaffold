using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace AP.Host.Desktop.Services;

/// <summary>
/// 系统托盘图标管理器
/// 提供最小化到托盘、托盘菜单、双击显示等功能
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private Window? _mainWindow;

    public TrayIconManager()
    {
        _notifyIcon = new NotifyIcon
        {
            Text = "自动化监控系统",
            Visible = true,
            Icon = SystemIcons.Application
        };

        _notifyIcon.DoubleClick += OnDoubleClick;

        var contextMenu = new ContextMenuStrip();
        contextMenu.Items.Add("显示主窗口", null, (_, _) => ShowMainWindow());
        contextMenu.Items.Add("重启", null, (_, _) => RestartApplication());
        contextMenu.Items.Add(new ToolStripSeparator());
        contextMenu.Items.Add("退出", null, (_, _) => ShutdownApplication());

        _notifyIcon.ContextMenuStrip = contextMenu;
    }

    /// <summary>
    /// 绑定主窗口，处理最小化到托盘
    /// </summary>
    public void Attach(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _mainWindow.StateChanged += OnMainWindowStateChanged;
        _notifyIcon.Text = _mainWindow.Title;
    }

    private void OnMainWindowStateChanged(object? sender, EventArgs e)
    {
        if (_mainWindow?.WindowState == WindowState.Minimized)
        {
            _mainWindow.Hide();
        }
    }

    private void OnDoubleClick(object? sender, EventArgs e)
    {
        ShowMainWindow();
    }

    private void ShowMainWindow()
    {
        // IsLoaded=false 表示窗口已关闭（如关闭流程中双击托盘），避免对已关闭窗口调用 Show 抛异常
        if (_mainWindow == null || !_mainWindow.IsLoaded) return;

        _mainWindow.Show();
        _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
        _mainWindow.Topmost = true;
        _mainWindow.Topmost = false;
    }

    private static void RestartApplication()
    {
        var fileName = Environment.ProcessPath ?? Application.ResourceAssembly.Location;
        // --restart：新进程将等待本进程释放单实例互斥体后再继续启动，避免双进程并存
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(fileName)
        {
            UseShellExecute = true,
            Arguments = "--restart"
        });
        Application.Current.Shutdown();
    }

    private static void ShutdownApplication()
    {
        Application.Current.Shutdown();
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
