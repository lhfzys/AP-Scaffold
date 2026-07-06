namespace AP.Infra.Report.Abstractions;

/// <summary>
/// 报表存储管理接口
/// 负责文件路径计算和目录管理
/// </summary>
public interface IReportStorage
{
    /// <summary>
    /// 获取报表文件路径
    /// </summary>
    /// <param name="date">报表日期</param>
    /// <param name="reportType">报表类型</param>
    /// <returns>完整文件路径</returns>
    string GetFilePath(DateTime date, string reportType);

    /// <summary>
    /// 确保目录存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    void EnsureDirectoryExists(string filePath);

    /// <summary>
    /// 删除文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否删除成功</returns>
    bool DeleteFile(string filePath);

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否存在</returns>
    bool FileExists(string filePath);

    /// <summary>
    /// 获取文件大小（字节）
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件大小</returns>
    long GetFileSize(string filePath);

    /// <summary>
    /// 清理空目录
    /// </summary>
    /// <param name="rootPath">根目录</param>
    void CleanupEmptyDirectories(string rootPath);
}