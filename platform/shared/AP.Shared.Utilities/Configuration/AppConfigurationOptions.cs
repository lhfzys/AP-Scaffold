namespace AP.Shared.Utilities.Configuration;

/// <summary>
/// 应用基础配置选项
/// </summary>
public class AppConfigurationOptions
{
    public const string SectionName = "AppConfiguration";

    public string CompanyName { get; set; } = "未配置公司";
    public string SoftwareName { get; set; } = "未配置软件";
    public string MachineId { get; set; } = "Unknown-Machine";
    public string MachineName { get; set; } = "未命名工位";
    public string LayoutMode { get; set; } = "Standard";
}
