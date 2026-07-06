namespace AP.Infra.Report.Entities;

/// <summary>
/// 通用报表数据模型
/// 业务插件将业务数据转换为此统一格式
/// </summary>
public class ReportData
{
    /// <summary>
    /// 报表类型标识
    /// </summary>
    public string ReportType { get; set; } = string.Empty;

    /// <summary>
    /// 报表显示名称
    /// </summary>
    public string ReportName { get; set; } = string.Empty;

    /// <summary>
    /// 报表日期
    /// </summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// 列标题
    /// </summary>
    public List<string> Headers { get; set; } = [];

    /// <summary>
    /// 数据行（每行是一个对象列表，对应各列的值）
    /// </summary>
    public List<List<object>> Rows { get; set; } = [];

    /// <summary>
    /// 汇总信息（如 总数、合格率 等）
    /// </summary>
    public Dictionary<string, object> Summary { get; set; } = [];

    /// <summary>
    /// 数据行数
    /// </summary>
    public int RowCount => Rows.Count;
}