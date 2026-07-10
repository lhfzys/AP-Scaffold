using AP.Host.Desktop.Bootstrapping;
using Serilog;
using System.Windows;

namespace AP.Host.Desktop;

public partial class App : Application
{
    private Bootstrapper? _bootstrapper;

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
        _bootstrapper = new Bootstrapper(appRole);
        _bootstrapper.Run();
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
            Log.CloseAndFlush();
        }

        base.OnExit(e);
    }
}
