using AP.Plugin.Sample.Configuration;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Configuration;

namespace AP.Plugin.Sample.ViewModels;

/// <summary>示例业务页 ViewModel：演示配置读取、命令和状态绑定。</summary>
public partial class SampleViewModel : ObservableObject
{
    [ObservableProperty] private string _message = "示例业务插件已加载";
    [ObservableProperty] private int _count;

    private readonly int _step;

    public SampleViewModel(IConfiguration configuration)
    {
        var options = configuration.GetSection(SampleOptions.SectionName).Get<SampleOptions>()
                      ?? new SampleOptions();
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
