using System.Reflection;
using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Attributes;

namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 插件描述符 (持有插件运行时的所有信息)
/// </summary>
public class PluginDescriptor
{
    /// <summary>
    /// 插件元数据 (ID, Version, Dependencies 等)
    /// </summary>
    public PluginMetadataAttribute Metadata { get; }

    /// <summary>
    /// 插件类型 (IPlugin 的具体实现类)
    /// </summary>
    public Type PluginType { get; }

    /// <summary>
    /// 插件所在的隔离上下文 (用于卸载)
    /// </summary>
    public PluginLoadContext LoadContext { get; }

    /// <summary>
    /// 插件程序集
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// 当前加载状态
    /// </summary>
    public bool IsLoaded { get; set; }

    /// <summary>
    /// 实例化后的插件对象 (仅在加载后可用)
    /// </summary>
    public IPlugin? Instance { get; set; }

    public PluginDescriptor(
        PluginMetadataAttribute metadata,
        Type pluginType,
        PluginLoadContext loadContext,
        Assembly assembly)
    {
        Metadata = metadata;
        PluginType = pluginType;
        LoadContext = loadContext;
        Assembly = assembly;
        IsLoaded = false;
    }
}