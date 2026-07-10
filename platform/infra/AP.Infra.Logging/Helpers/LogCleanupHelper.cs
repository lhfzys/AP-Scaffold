using Serilog;

namespace AP.Infra.Logging.Helpers;

/// <summary>
/// 日志启动时一次性清理工具
/// 在应用启动时调用，清理超过保留天数的旧日志文件
/// 不启动后台服务，仅在启动时执行一次，零运行时开销
/// </summary>
public static class LogCleanupHelper
{
    /// <summary>
    /// 清理超过保留天数的日志文件
    /// </summary>
    /// <param name="logPath">日志目录路径</param>
    /// <param name="maxRetainDays">最大保留天数</param>
    public static void CleanupIfNeeded(string logPath, int maxRetainDays)
    {
        try
        {
            if (!Directory.Exists(logPath))
            {
                return;
            }

            var cutoff = DateTime.Now.AddDays(-maxRetainDays);

            // 查找所有日志文件（包括滚动产生的带序号的文件）
            var logFiles = Directory.GetFiles(logPath, "log-*.txt")
                .Select(f => new FileInfo(f))
                .Where(f => f.CreationTime < cutoff)
                .OrderBy(f => f.CreationTime)
                .ToList();

            if (logFiles.Count == 0)
            {
                return;
            }

            var deletedCount = 0;
            var freedBytes = 0L;

            foreach (var file in logFiles)
            {
                try
                {
                    freedBytes += file.Length;
                    file.Delete();
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    // 单个文件删除失败不影响其他文件
                    Log.Warning(ex, "启动时清理日志文件失败: {File}", file.Name);
                }
            }

            if (deletedCount > 0)
            {
                var freedMb = freedBytes / 1024.0 / 1024.0;
                Log.Information(
                    "启动时日志清理完成: 删除 {Count} 个过期文件 (>{Days}天), 释放 {Size:F1}MB",
                    deletedCount, maxRetainDays, freedMb);
            }
        }
        catch (Exception ex)
        {
            // 清理失败绝不影响主流程启动
            Log.Warning(ex, "启动时日志清理发生异常，已跳过");
        }
    }
}