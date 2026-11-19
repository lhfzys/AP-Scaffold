using AP.Plugin.Sample.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace AP.Plugin.Sample.ViewModels;

/// <summary>插件首页 ViewModel：演示配置读取与命令。</summary>
public partial class PluginViewModel : ObservableObject
{
    [ObservableProperty] private string _message = "新插件已加载";
    [ObservableProperty] private int _count;

    private readonly int _step;

    public PluginViewModel(IConfiguration configuration)
    {
        var options = configuration.GetSection(PluginOptions.SectionName).Get<PluginOptions>()
                      ?? new PluginOptions();
        _message = options.Greeting;
        _step = Math.Max(1, options.Step);
    }

    [RelayCommand]
    private void Increment()
    {
        Count += _step;
        Message = $"已累加 {Count}（步长 {_step}）";
    }

    [RelayCommand]
    private void Reset()
    {
        Count = 0;
        Message = "已重置";
    }
}
