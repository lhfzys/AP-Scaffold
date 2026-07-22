using AP.Contracts.Report.Models;
using AP.Infra.Report.Abstractions;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;

namespace AP.Infra.Report.Services;

/// <summary>
/// 基于 MiniExcel 的 Excel 导出实现
/// </summary>
public class ExcelExporter : IExcelExporter
{
    private readonly ILogger<ExcelExporter> _logger;

    public ExcelExporter(ILogger<ExcelExporter> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task ExportAsync(ReportData data, string filePath, string? templatePath = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        try
        {
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 使用临时文件，完成后原子重命名
            var tempFilePath = filePath + ".tmp";

            // 构建数据行
            var excelRows = BuildExcelRows(data);

            // 导出到临时文件
            using (var stream = File.Create(tempFilePath))
            {
                stream.SaveAs(excelRows, sheetName: data.ReportName);
            }

            // 原子重命名
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            File.Move(tempFilePath, filePath);

            _logger.LogInformation("报表导出成功: {FilePath}, 行数: {RowCount}", filePath, data.RowCount);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "报表导出失败: {FilePath}", filePath);
            throw;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 将 ReportData 转换为 MiniExcel 行数据
    /// </summary>
    private static List<Dictionary<string, object>> BuildExcelRows(ReportData data)
    {
        var excelRows = new List<Dictionary<string, object>>
        {
            // 第一行为表头
            data.Headers.ToDictionary(h => h, h => (object)h)
        };

        // 数据行
        for (var i = 0; i < data.Rows.Count; i++)
        {
            var row = new Dictionary<string, object>();
            for (var j = 0; j < data.Headers.Count && j < data.Rows[i].Count; j++)
            {
                row[data.Headers[j]] = data.Rows[i][j];
            }
            excelRows.Add(row);
        }

        return excelRows;
    }
}