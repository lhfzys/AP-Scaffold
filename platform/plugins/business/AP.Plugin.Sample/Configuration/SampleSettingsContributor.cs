using AP.Plugin.Sample.Configuration;
using AP.Plugin.Sample.ViewModels;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.Sample.Configuration;

/// <summary>示例设置贡献者：配置中心“示例”分组下的一页。</summary>
public sealed class SampleSettingsContributor : ISettingsContributor
{
    public string Category => "示例";
    public string Title => "示例业务设置";
    public string? IconKind => "Flask";
    public int Order => 900;
    public string ConfigurationSection => SampleOptions.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
        => ActivatorUtilities.CreateInstance<SampleSettingsViewModel>(serviceProvider);
}
