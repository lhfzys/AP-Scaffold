using AP.Core.PluginFramework.Loading;

namespace AP.Core.Lifecycle;

/// <summary>
/// 插件执行上下文 (用于在调用链中传递当前插件信息)
/// </summary>
public class PluginExecutionContext
{
    /// <summary>
    /// 当前正在执行的插件描述符
    /// </summary>
    public PluginDescriptor Descriptor { get; }

    /// <summary>
    /// 插件私有的服务提供者 (Scope)
    /// </summary>
    public IServiceProvider ServiceProvider { get; }

    public PluginExecutionContext(PluginDescriptor descriptor, IServiceProvider serviceProvider)
    {
        Descriptor = descriptor;
        ServiceProvider = serviceProvider;
    }

    public static PluginExecutionContext Create(PluginDescriptor descriptor, IServiceProvider serviceProvider)
    {
        return new PluginExecutionContext(descriptor, serviceProvider);
    }
}