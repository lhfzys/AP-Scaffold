using AP.Contracts.Report.Abstractions;
using AP.Contracts.Report.Models;
using AP.Infra.Report.Abstractions;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Report.Services;

/// <summary>
/// 报表中心服务实现
/// </summary>
public class ReportCenterService : IReportCenterService
{
    private readonly ReportService _reportService;
    private readonly IReportRepository _repository;
    private readonly IReportStorage _storage;
    private readonly ILogger<ReportCenterService> _logger;

    public ReportCenterService(
        ReportService reportService,
        IReportRepository repository,
        IReportStorage storage,
        ILogger<ReportCenterService> logger)
    {
        _reportService = reportService;
        _repository = repository;
        _storage = storage;
        _logger = logger;
    }

    public Task<IReadOnlyList<ReportTypeInfo>> GetReportTypesAsync(CancellationToken ct = default)
    {
        var types = _reportService.GetProviders()
            .Select(p => new ReportTypeInfo
            {
                ReportType = p.ReportType,
                ReportName = p.ReportName
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<ReportTypeInfo>>(types);
    }

    public async Task<IReadOnlyList<ReportArchiveDto>> GetArchivesAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        string? reportType = null,
        CancellationToken ct = default)
    {
        var from = startDate ?? DateTime.MinValue;
        var to = endDate ?? DateTime.MaxValue;

        var archives = await _repository.GetByDateRangeAsync(from, to, ct);

        if (!string.IsNullOrWhiteSpace(reportType))
        {
            archives = archives
                .Where(a => a.ReportType.Equals(reportType, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return archives
            .OrderByDescending(a => a.ReportDate)
            .ThenBy(a => a.ReportType)
            .Select(MapToDto)
            .ToList();
    }

    public async Task<string> GenerateAsync(string reportType, DateTime date, CancellationToken ct = default)
    {
        return await _reportService.GenerateReportAsync(reportType, date, false, ct);
    }

    public async Task<string> RegenerateAsync(string reportType, DateTime date, CancellationToken ct = default)
    {
        return await _reportService.RegenerateReportAsync(reportType, date, ct);
    }

    public async Task OpenAsync(string archiveId, CancellationToken ct = default)
    {
        var archive = await _repository.GetByIdAsync(archiveId, ct);
        if (archive == null)
        {
            throw new InvalidOperationException("归档记录不存在");
        }

        if (!_storage.FileExists(archive.FilePath))
        {
            throw new InvalidOperationException("报表文件不存在或已被清理");
        }

        // 使用默认程序打开 Excel 文件
        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = archive.FilePath,
            UseShellExecute = true
        };
        System.Diagnostics.Process.Start(psi);
    }

    public async Task<string> ExportAsync(string archiveId, string destinationDirectory, CancellationToken ct = default)
    {
        var archive = await _repository.GetByIdAsync(archiveId, ct);
        if (archive == null)
        {
            throw new InvalidOperationException("归档记录不存在");
        }

        if (!_storage.FileExists(archive.FilePath))
        {
            throw new InvalidOperationException("报表文件不存在或已被清理");
        }

        if (!Directory.Exists(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        var fileName = Path.GetFileName(archive.FilePath);
        var destPath = Path.Combine(destinationDirectory, fileName);
        File.Copy(archive.FilePath, destPath, overwrite: true);

        return destPath;
    }

    private static ReportArchiveDto MapToDto(Entities.ReportArchive archive)
    {
        return new ReportArchiveDto
        {
            Id = archive.Id,
            ReportDate = archive.ReportDate,
            ReportType = archive.ReportType,
            ReportName = archive.ReportName,
            FilePath = archive.FilePath,
            RecordCount = archive.RecordCount,
            FileSize = archive.FileSize,
            GeneratedAt = archive.GeneratedAt,
            Status = archive.Status.ToString(),
            FailureReason = archive.FailureReason
        };
    }
}
