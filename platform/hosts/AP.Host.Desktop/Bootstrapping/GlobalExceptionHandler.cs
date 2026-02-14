using Serilog;
using System.Windows;
using System.Windows.Threading;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 全局异常捕获处理器
/// </summary>
public static class GlobalExceptionHandler
{
    /// <summary>
    /// 初始化全局异常捕获
    /// </summary>
    public static void Initialize()
    {
        // 1. UI 线程异常
        Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // 2. 后台 Task 异常
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        // 3. 致命异常 (AppDomain)
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "💥 [UI线程] 发生未捕获异常");

        e.Handled = true;

        var errorMsg = $"程序遇到问题，但已拦截。建议联系管理员。\n\n错误详情: {e.Exception.Message}";
        MessageBox.Show(errorMsg, "系统警告", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "💥 [后台线程] 发生未捕获异常");
        e.SetObserved();
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "💀 [致命错误] 系统即将终止");

        MessageBox.Show($"发生致命错误，程序即将退出。\n{ex?.Message}", "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}