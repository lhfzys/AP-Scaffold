using AP.Plugin.Sample.Configuration;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace AP.Plugin.Sample.ViewModels;

/// <summary>示例业务设置编辑器：演示 ISettingsContributor 的配置保存/校验。</summary>
public partial class SampleSettingsViewModel : ViewModelBase, ISettingsEditorViewModel
{
    [ObservableProperty] private string _greeting = "示例业务插件已加载";
    [ObservableProperty] private int _step = 1;

    public bool RequiresRestart => true;

    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(SampleOptions.SectionName).Get<SampleOptions>()
                      ?? new SampleOptions();
        Greeting = options.Greeting;
        Step = options.Step;
    }

    public IEnumerable<string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Greeting))
            yield return "欢迎语不能为空";
        if (Step <= 0)
            yield return "步长必须大于 0";
    }

    public object GetConfigurationValue()
        => new SampleOptions { Greeting = Greeting, Step = Step };
}
