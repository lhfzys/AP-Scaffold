using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.Plc.Mitsubishi.Configuration;

/// <summary>
/// 三菱 PLC 配置贡献者
/// </summary>
public class MitsubishiPlcConfigurationContributor : ISettingsContributor
{
    public string Category => "硬件";
    public string Title => "三菱 PLC 配置";
    public string? IconKind => "Cpu32Bit";
    public int Order => 100;
    public string ConfigurationSection => MitsubishiPlcOptions.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<MitsubishiPlcConfigurationEditorViewModel>(serviceProvider);
    }
}
