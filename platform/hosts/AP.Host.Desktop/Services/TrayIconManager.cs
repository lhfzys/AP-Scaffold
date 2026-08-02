using System.Windows;
using System.Windows.Forms;
using Application = System.Windows.Application;

namespace AP.Host.Desktop.Services;

/// <summary>
/// 系统托盘图标管理器
/// 托盘图标常驻（菜单：显示主窗口/重启/退出，双击显示）。
/// 最小化为标准行为（回任务栏），不隐藏到托盘（2026-08-01 起，A 方案）。
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
            // 跟随 exe 嵌入图标（ApplicationIcon），不再用系统默认图标
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                System.Diagnostics.Process.GetCurrentProcess().MainModule!.FileName)
                    ?? SystemIcons.Application
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
    /// 绑定主窗口（同步托盘提示文本；最小化不再拦截，标准回任务栏）
    /// </summary>
    public void Attach(Window mainWindow)
    {
        _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
        _notifyIcon.Text = _mainWindow.Title;
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
