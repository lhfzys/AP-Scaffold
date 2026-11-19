namespace AP.Plugin.Sample.Configuration;

/// <summary>插件配置（可在系统配置中心编辑）。</summary>
public class PluginOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Sample";

    public string Greeting { get; set; } = "新插件已加载";
    public int Step { get; set; } = 1;
}
