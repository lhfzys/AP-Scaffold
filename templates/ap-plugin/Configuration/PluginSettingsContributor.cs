using AP.Plugin.Sample.Configuration;
using AP.Plugin.Sample.ViewModels;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.Sample.Configuration;

/// <summary>设置贡献者：配置中心“业务”分组下的一页。</summary>
public sealed class PluginSettingsContributor : ISettingsContributor
{
    public string Category => "业务";
    public string Title => "插件设置";
    public string? IconKind => "Cog";
    public int Order => 900;
    public string ConfigurationSection => PluginOptions.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
        => ActivatorUtilities.CreateInstance<PluginSettingsViewModel>(serviceProvider);
}
