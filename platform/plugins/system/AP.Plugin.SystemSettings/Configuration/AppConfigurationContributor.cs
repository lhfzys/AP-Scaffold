using AP.Plugin.SystemSettings.Editors;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.Utilities.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.SystemSettings.Configuration;

/// <summary>
/// 应用基础配置贡献者
/// </summary>
public class AppConfigurationContributor : ISettingsContributor
{
    public string Category => "系统";
    public string Title => "应用基础信息";
    public string? IconKind => "Application";
    public int Order => 100;
    public string ConfigurationSection => AppConfigurationOptions.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<AppConfigurationEditorViewModel>(serviceProvider);
    }
}
