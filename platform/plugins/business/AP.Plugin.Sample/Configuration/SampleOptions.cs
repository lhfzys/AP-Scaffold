namespace AP.Plugin.Sample.Configuration;

/// <summary>示例业务插件配置（可在系统配置中心编辑）。</summary>
public class SampleOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Sample";

    public string Greeting { get; set; } = "示例业务插件已加载";
    public int Step { get; set; } = 1;
}
