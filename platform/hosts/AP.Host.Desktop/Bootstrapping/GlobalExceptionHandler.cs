using Serilog;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

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
    /// 崩溃日志文件路径
    /// </summary>
    private static readonly string CrashLogPath = Path.Combine(
        AppContext.BaseDirectory,
        "logs",
        $"crash-{DateTime.Now:yyyyMMdd}.log");

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
    /// 安全地写入崩溃日志（不依赖 Serilog 是否已完成初始化）
    /// </summary>
    private static void WriteCrashLog(string category, Exception? ex)
    {
        try
        {
            var logDir = Path.GetDirectoryName(CrashLogPath);
            if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{category}]");
            if (ex != null)
            {
                builder.AppendLine($"Type: {ex.GetType().FullName}");
                builder.AppendLine($"Message: {ex.Message}");
                builder.AppendLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                    builder.AppendLine($"InnerException: {ex.InnerException}");
            }
            else
            {
                builder.AppendLine("No exception object available.");
            }
            builder.AppendLine(new string('-', 60));

            File.AppendAllText(CrashLogPath, builder.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 崩溃日志写入失败时不应再抛异常
        }
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
            WriteCrashLog("FATAL-UI", ex);

            e.Handled = false; // 不标记为已处理，让应用正常崩溃

            // 主动退出，避免程序处于不一致状态
            Environment.Exit(1);
            return;
        }

        // 可恢复异常：记录日志，标记为已处理，不弹窗以避免打断自动化流程
        Log.Error(ex, "💥 [UI线程] 发生可恢复异常");
        WriteCrashLog("RECOVERABLE-UI", ex);
        e.Handled = true;
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
            WriteCrashLog("FATAL-BACKGROUND", ex);
            e.SetObserved();

            Environment.Exit(1);
            return;
        }

        // 可恢复异常：记录日志，标记为已观察
        Log.Error(ex, "💥 [后台线程] 发生未捕获异常");
        WriteCrashLog("RECOVERABLE-BACKGROUND", ex);
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
        WriteCrashLog($"APPDOMAIN-FATAL (IsTerminating={e.IsTerminating})", ex);
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
