using System.Reflection;
using AP.Core.PluginFramework.Abstractions;
using AP.Core.PluginFramework.Attributes;

namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 程序集扫描器 (查找合法插件类型)
/// </summary>
public class AssemblyScanner
{
    /// <summary>
    /// 从程序集中扫描实现了 IPlugin 的类型
    /// </summary>
    public static Type? ScanForPluginType(Assembly assembly)
    {
        return assembly.GetExportedTypes()
            .FirstOrDefault(t =>
                typeof(IPlugin).IsAssignableFrom(t) &&
                !t.IsAbstract &&
                !t.IsInterface);
    }

    /// <summary>
    /// 获取插件的元数据
    /// </summary>
    public static PluginMetadataAttribute? GetMetadata(Type pluginType)
    {
        return pluginType.GetCustomAttribute<PluginMetadataAttribute>();
    }
}