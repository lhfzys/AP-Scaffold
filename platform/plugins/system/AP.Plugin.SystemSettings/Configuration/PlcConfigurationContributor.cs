using AP.Plugin.SystemSettings.Editors;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.SystemSettings.Configuration;

/// <summary>
/// PLC 统一配置贡献者。
/// </summary>
public class PlcConfigurationContributor : ISettingsContributor
{
    public string Category => "硬件";
    public string Title => "PLC 配置";
    public string? IconKind => "Cpu32Bit";
    public int Order => 100;
    public string ConfigurationSection => AP.Contracts.Hardware.Models.PlcOptions.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<PlcConfigurationEditorViewModel>(serviceProvider);
    }
}
