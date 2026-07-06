using AP.Infra.Report.Abstractions;
using AP.Infra.Report.Configuration;
using AP.Infra.Report.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表核心服务
/// 提供报表生成、补档、查询等功能
/// </summary>
public class ReportService
{
    private readonly IEnumerable<IReportDataProvider> _providers;
    private readonly IExcelExporter _exporter;
    private readonly IReportStorage _storage;
    private readonly IReportRepository _repository;
    private readonly ReportOptions _options;
    private readonly ILogger<ReportService> _logger;

    public ReportService(
        IEnumerable<IReportDataProvider> providers,
        IExcelExporter exporter,
        IReportStorage storage,
        IReportRepository repository,
        IOptions<ReportOptions> options,
        ILogger<ReportService> logger)
    {
        _providers = providers;
        _exporter = exporter;
        _storage = storage;
        _repository = repository;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// 获取所有已注册的报表提供者
    /// </summary>
    public IReadOnlyList<IReportDataProvider> GetProviders()
    {
        return _providers.ToList().AsReadOnly();
    }

    /// <summary>
    /// 生成指定类型的报表
    /// </summary>
    /// <param name="reportType">报表类型</param>
    /// <param name="date">报表日期</param>
    /// <param name="forceRegenerate">是否强制重新生成（补档）</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的文件路径</returns>
    public async Task<string> GenerateReportAsync(string reportType, DateTime date, bool forceRegenerate = false, CancellationToken ct = default)
    {
        var provider = GetProvider(reportType);
        if (provider == null)
        {
            throw new ArgumentException($"未找到报表类型: {reportType}");
        }

        // 检查是否已存在归档记录
        var existingArchive = await _repository.GetByDateAndTypeAsync(date, reportType, ct);
        if (existingArchive != null && !forceRegenerate)
        {
            _logger.LogInformation("报表已存在，跳过生成: {Type}, {Date}", reportType, date);
            return existingArchive.FilePath;
        }

        // 获取报表数据
        _logger.LogInformation("开始生成报表: {Type}, {Date}", reportType, date);
        var data = await provider.GetReportDataAsync(date, ct);

        // 计算文件路径
        var filePath = _storage.GetFilePath(date, reportType);
        _storage.EnsureDirectoryExists(filePath);

        // 获取模板路径
        var templatePath = provider.GetTemplatePath() ?? _options.Storage.DefaultTemplatePath;

        // 创建归档记录
        var archive = new ReportArchive
        {
            ReportDate = date,
            ReportType = reportType,
            ReportName = provider.ReportName,
            FilePath = filePath,
            RecordCount = data.RowCount,
            GeneratedAt = DateTime.Now,
            Status = ArchiveStatus.Failed // 先标记为失败，成功后更新
        };

        try
        {
            // 导出 Excel
            await _exporter.ExportAsync(data, filePath, templatePath, ct);

            // 更新归档记录
            archive.FileSize = _storage.GetFileSize(filePath);
            archive.Status = ArchiveStatus.Success;

            if (existingArchive != null)
            {
                // 更新已有记录（补档场景）
                archive.Id = existingArchive.Id;
                await _repository.UpdateAsync(archive, ct);
            }
            else
            {
                await _repository.AddAsync(archive, ct);
            }

            _logger.LogInformation("报表生成成功: {Type}, {Date}, 文件: {Path}, 行数: {Count}",
                reportType, date, filePath, data.RowCount);

            return filePath;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 记录失败
            archive.FailureReason = ex.Message;
            archive.Status = ArchiveStatus.Failed;

            if (existingArchive != null)
            {
                archive.Id = existingArchive.Id;
                await _repository.UpdateAsync(archive, ct);
            }
            else
            {
                await _repository.AddAsync(archive, ct);
            }

            _logger.LogError(ex, "报表生成失败: {Type}, {Date}", reportType, date);
            throw;
        }
    }

    /// <summary>
    /// 生成所有类型的日报
    /// </summary>
    /// <param name="date">报表日期</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>报表类型到文件路径的映射</returns>
    public async Task<Dictionary<string, string>> GenerateDailyReportsAsync(DateTime date, CancellationToken ct = default)
    {
        var results = new Dictionary<string, string>();
        var providers = GetProvidersToArchive();

        foreach (var provider in providers)
        {
            try
            {
                var path = await GenerateReportAsync(provider.ReportType, date, false, ct);
                results[provider.ReportType] = path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "生成日报失败: {Type}", provider.ReportType);
                results[provider.ReportType] = $"ERROR: {ex.Message}";
            }
        }

        return results;
    }

    /// <summary>
    /// 补档：重新生成指定日期的报表
    /// </summary>
    /// <param name="reportType">报表类型</param>
    /// <param name="date">报表日期</param>
    /// <param name="ct">取消令牌</param>
    /// <returns>生成的文件路径</returns>
    public async Task<string> RegenerateReportAsync(string reportType, DateTime date, CancellationToken ct = default)
    {
        _logger.LogInformation("开始补档: {Type}, {Date}", reportType, date);
        return await GenerateReportAsync(reportType, date, forceRegenerate: true, ct);
    }

    /// <summary>
    /// 查询指定日期的归档记录
    /// </summary>
    public async Task<List<ReportArchive>> GetArchivesByDateAsync(DateTime date, CancellationToken ct = default)
    {
        return await _repository.GetByDateRangeAsync(date, date, ct);
    }

    /// <summary>
    /// 查询日期范围内的归档记录
    /// </summary>
    public async Task<List<ReportArchive>> GetArchivesByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await _repository.GetByDateRangeAsync(startDate, endDate, ct);
    }

    /// <summary>
    /// 获取指定报表提供者
    /// </summary>
    private IReportDataProvider? GetProvider(string reportType)
    {
        return _providers.FirstOrDefault(p => p.ReportType.Equals(reportType, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// 获取需要归档的报表提供者列表
    /// </summary>
    private IEnumerable<IReportDataProvider> GetProvidersToArchive()
    {
        var configuredTypes = _options.Archive.ReportTypes;

        if (configuredTypes.Count > 0)
        {
            return _providers.Where(p => configuredTypes.Contains(p.ReportType));
        }

        return _providers;
    }
}