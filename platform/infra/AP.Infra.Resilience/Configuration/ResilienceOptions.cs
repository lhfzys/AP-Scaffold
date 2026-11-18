namespace AP.Infra.Resilience.Configuration;

/// <summary>
/// 韧性策略配置选项 (对应 appsettings.json 中的 Resilience 节点)
/// </summary>
public class ResilienceOptions
{
    public const string SectionName = "Resilience";

    /// <summary>
    /// 数据库最大重试次数
    /// </summary>
    public int DatabaseRetryCount { get; set; } = 3;

    /// <summary>
    /// PLC 连接重试次数
    /// </summary>
    public int PlcRetryCount { get; set; } = 5;
}