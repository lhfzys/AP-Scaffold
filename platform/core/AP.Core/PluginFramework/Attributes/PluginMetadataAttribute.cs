using AP.Core.Enums;

namespace AP.Core.PluginFramework.Attributes;

/// <summary>
/// 插件元数据特性 (用于描述插件的基本信息)
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class PluginMetadataAttribute : Attribute
{
    /// <summary>
    /// 插件唯一标识 (严禁重复)
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// 插件显示名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 插件版本
    /// </summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>
    /// 支持的运行角色 (默认全支持)
    /// </summary>
    public AppRole SupportedRoles { get; set; } = AppRole.All;

    /// <summary>
    /// 依赖的其他插件 ID 列表
    /// </summary>
    public string[] Dependencies { get; set; } = Array.Empty<string>();

    /// <summary>
    /// 加载优先级 (数值越小越先加载)
    /// </summary>
    public int Priority { get; set; } = 100;

    /// <summary>
    /// 是否必需 (必需插件加载失败会导致应用退出)
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="id">插件唯一标识 (建议使用命名空间格式，如 AP.Plugin.Xxx)</param>
    public PluginMetadataAttribute(string id)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
    }
}