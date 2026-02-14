namespace AP.Host.Desktop.Configuration;

public class AppConfigurationOptions
{
    public const string SectionName = "AppConfiguration";

    public string CompanyName { get; set; } = "未配置公司";
    public string SoftwareName { get; set; } = "未配置软件";
    public string MachineId { get; set; } = "Unknown-Machine";
    public string MachineName { get; set; } = "未命名工位";
}