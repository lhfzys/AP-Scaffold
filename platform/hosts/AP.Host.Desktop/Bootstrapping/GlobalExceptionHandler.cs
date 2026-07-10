using Serilog;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 全局异常捕获处理器
/// 区分可恢复异常和不可恢复异常，确保工业现场系统稳定性
/// </summary>
public static class GlobalExceptionHandler
{
    /// <summary>
    /// 不可恢复异常类型列表
    /// 这些异常表示程序状态已严重损坏，必须退出
    /// </summary>
    private static readonly Type[] _fatalExceptionTypes =
    [
        typeof(StackOverflowException),
        typeof(OutOfMemoryException),
        typeof(AccessViolationException),
        typeof(SEHException),           // 结构化异常处理（非托管代码崩溃）
        typeof(InvalidProgramException),
        typeof(MissingMethodException),
        typeof(MissingFieldException),
        typeof(TypeLoadException),
        typeof(BadImageFormatException)
    ];

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

    /// <summary>
    /// UI 线程异常处理
    /// 区分可恢复和不可恢复异常
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        var ex = e.Exception;

        // 检查是否为不可恢复异常
        if (IsFatalException(ex))
        {
            Log.Fatal(ex, "💀 [UI线程] 发生不可恢复异常，程序即将退出");

            e.Handled = false; // 不标记为已处理，让应用正常崩溃

            MessageBox.Show(
                $"发生严重错误，程序无法继续运行。\n\n错误类型: {ex.GetType().Name}\n错误详情: {ex.Message}\n\n程序即将退出，请检查日志获取详细信息。",
                "致命错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // 主动退出，避免程序处于不一致状态
            Environment.Exit(1);
            return;
        }

        // 可恢复异常：记录日志，标记为已处理，显示警告
        Log.Error(ex, "💥 [UI线程] 发生可恢复异常");

        e.Handled = true;

        var errorMsg = $"程序遇到问题，但已拦截。建议联系管理员。\n\n错误详情: {ex.Message}";
        MessageBox.Show(errorMsg, "系统警告", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <summary>
    /// 后台 Task 异常处理
    /// </summary>
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        var ex = e.Exception?.InnerException ?? e.Exception;

        // 检查是否为不可恢复异常
        if (IsFatalException(ex))
        {
            Log.Fatal(ex, "💀 [后台线程] 发生不可恢复异常，程序即将退出");
            e.SetObserved();

            // 在 UI 线程显示错误并退出
            Application.Current?.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"后台任务发生严重错误，程序无法继续运行。\n\n错误类型: {ex?.GetType().Name}\n错误详情: {ex?.Message}",
                    "致命错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Environment.Exit(1);
            });
            return;
        }

        // 可恢复异常：记录日志，标记为已观察
        Log.Error(ex, "💥 [后台线程] 发生未捕获异常");
        e.SetObserved();
    }

    /// <summary>
    /// AppDomain 级别的未处理异常
    /// 这些异常通常是致命的
    /// </summary>
    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Fatal(ex, "💀 [致命错误] 系统即将终止 (IsTerminating: {IsTerminating})", e.IsTerminating);

        MessageBox.Show(
            $"发生致命错误，程序即将退出。\n\n错误类型: {ex?.GetType().Name}\n错误详情: {ex?.Message}\n\n请检查日志获取详细信息。",
            "致命错误",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    /// <summary>
    /// 判断异常是否为不可恢复类型
    /// </summary>
    private static bool IsFatalException(Exception? ex)
    {
        if (ex == null) return false;

        var exType = ex.GetType();

        // 直接匹配致命异常类型
        if (_fatalExceptionTypes.Contains(exType))
            return true;

        // 检查内部异常（递归）
        if (ex.InnerException != null)
            return IsFatalException(ex.InnerException);

        // AggregateException 需要检查所有内部异常
        if (ex is AggregateException aggEx)
        {
            foreach (var inner in aggEx.InnerExceptions)
            {
                if (IsFatalException(inner))
                    return true;
            }
        }

        return false;
    }
}
