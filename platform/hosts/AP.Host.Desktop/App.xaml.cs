using AP.Host.Desktop.Bootstrapping;
using AP.Host.Desktop.Services;
using AP.Host.Desktop.Views;
using Serilog;
using System.Windows;

namespace AP.Host.Desktop;

public partial class App : System.Windows.Application
{
    private Bootstrapper? _bootstrapper;
    private SplashWindow? _splashWindow;
    private TrayIconManager? _trayIconManager;
    private Mutex? _appMutex;

    public App()
    {
        GlobalExceptionHandler.Initialize();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 0. 持有命名互斥体（供安装程序 Inno Setup AppMutex 检测应用正在运行，防止运行中覆盖安装）
        _appMutex = new Mutex(false, "AP.SCAFFOLD.PLATFORM.RUNNING");

        // 1. 解析运行角色 (默认为 Standalone)
        var appRole = RoleResolver.Resolve(e.Args);

        // 2. 显示启动画面
        _splashWindow = new SplashWindow();
        _splashWindow.Show();

        // 3. 启动引导器
        _bootstrapper = new Bootstrapper(appRole, _splashWindow);
        _bootstrapper.Run();

        // 4. 初始化系统托盘
        _trayIconManager = new TrayIconManager();
        if (Current.MainWindow != null)
        {
            _trayIconManager.Attach(Current.MainWindow);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Log.Information("应用程序正在退出...");

            // 优雅停止所有插件和服务（在关闭日志之前）
            if (_bootstrapper != null)
            {
                _bootstrapper.ShutdownAsync().GetAwaiter().GetResult();
            }
        }
        catch (Exception ex)
        {
            // 退出阶段的异常不应阻止程序关闭
            Log.Error(ex, "退出过程中发生异常");
        }
        finally
        {
            _trayIconManager?.Dispose();
            _appMutex?.Dispose();
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }
}
