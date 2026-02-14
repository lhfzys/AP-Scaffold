using AP.Core.Capability;

namespace AP.Core.PluginFramework.Attributes;

/// <summary>
/// 声明插件运行所需的必要能力
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public class RequiresCapabilitiesAttribute(PluginCapabilities capabilities) : Attribute
{
    public PluginCapabilities Capabilities { get; } = capabilities;
}