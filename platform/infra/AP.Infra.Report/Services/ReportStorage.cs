using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表存储管理实现
/// </summary>
public class ReportStorage : IReportStorage
{
    private readonly ReportOptions _options;
    private readonly ILogger<ReportStorage> _logger;

    public ReportStorage(IOptions<ReportOptions> options, ILogger<ReportStorage> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string GetFilePath(DateTime date, string reportType)
    {
        var format = _options.Storage.PathFormat;

        var path = format
            .Replace("{year}", date.ToString("yyyy"))
            .Replace("{month}", date.ToString("MM"))
            .Replace("{date}", date.ToString("yyyy-MM-dd"))
            .Replace("{type}", reportType);

        return Path.Combine(_options.Storage.RootPath, path);
    }

    /// <inheritdoc />
    public void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _logger.LogDebug("创建目录: {Directory}", directory);
        }
    }

    /// <inheritdoc />
    public bool DeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogDebug("删除文件: {FilePath}", filePath);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "删除文件失败: {FilePath}", filePath);
            return false;
        }
    }

    /// <inheritdoc />
    public bool FileExists(string filePath)
    {
        return File.Exists(filePath);
    }

    /// <inheritdoc />
    public long GetFileSize(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return 0;
        }

        var fileInfo = new FileInfo(filePath);
        return fileInfo.Length;
    }

    /// <inheritdoc />
    public void CleanupEmptyDirectories(string rootPath)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        try
        {
            // 递归清理空目录（从最深层开始）
            CleanupEmptyDirectoriesRecursive(rootPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理空目录失败: {RootPath}", rootPath);
        }
    }

    private void CleanupEmptyDirectoriesRecursive(string path)
    {
        // 先处理子目录
        foreach (var subDir in Directory.GetDirectories(path))
        {
            CleanupEmptyDirectoriesRecursive(subDir);
        }

        // 检查当前目录是否为空
        if (!Directory.EnumerateFileSystemEntries(path).Any())
        {
            Directory.Delete(path);
            _logger.LogDebug("删除空目录: {Path}", path);
        }
    }
}