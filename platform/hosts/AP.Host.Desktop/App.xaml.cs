using AP.Host.Desktop.Bootstrapping;
using Serilog;
using System.Windows;

namespace AP.Host.Desktop;

public partial class App : Application
{
    public App()
    {
        GlobalExceptionHandler.Initialize();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 1. 解析运行角色 (默认为 Standalone)
        var appRole = RoleResolver.Resolve(e.Args);

        // 2. 启动引导器
        var bootstrapper = new Bootstrapper(appRole);
        bootstrapper.Run();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("应用程序正在退出...");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}