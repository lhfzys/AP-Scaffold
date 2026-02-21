#region

using System.Windows.Media;
using AP.Plugin.AirtightnessCheck.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;

#endregion

namespace AP.Plugin.AirtightnessCheck.ViewModels;

public partial class AirtightnessViewModel : ViewModelBase
{
    private readonly AirtightnessOptions _options;

    // --- 核心显示数据 ---
    [ObservableProperty] private string _testStateText = "等待测试";
    [ObservableProperty] private Brush _stateColor = Brushes.Gray;

    [ObservableProperty] private double _currentPressure = 0.0;
    [ObservableProperty] private double _leakRate = 0.0;

    // --- 参数显示 ---
    [ObservableProperty] private double _targetPressure;
    [ObservableProperty] private double _alarmLeakRate;
    [ObservableProperty] private string _currentRecipe;

    // --- UI 状态控制 ---
    [ObservableProperty] private bool _isTesting;

    public AirtightnessViewModel(IOptions<AirtightnessOptions> options)
    {
        _options = options.Value;

        // 从配置加载参数
        TargetPressure = _options.StandardPressure;
        AlarmLeakRate = _options.MaxLeakRate;
        CurrentRecipe = _options.DefaultRecipe;
    }

    // 模拟开始测试
    [RelayCommand(CanExecute = nameof(CanStartTest))]
    private async Task StartTestAsync()
    {
        IsTesting = true;
        StartTestCommand.NotifyCanExecuteChanged();

        TestStateText = "充气中...";
        StateColor = Brushes.DodgerBlue; // 蓝色代表运行中
        CurrentPressure = 0;
        LeakRate = 0;

        // 模拟充气过程
        for (var i = 0; i <= 10; i++)
        {
            CurrentPressure = TargetPressure * (i / 10.0);
            await Task.Delay(200);
        }

        TestStateText = "保压与测量中...";
        StateColor = Brushes.Orange;

        // 模拟测量泄漏
        var random = new Random();
        for (var i = 0; i < 15; i++)
        {
            LeakRate = random.NextDouble() * 60; // 模拟波动 0~60
            await Task.Delay(200);
        }

        // 判定结果
        IsTesting = false;
        StartTestCommand.NotifyCanExecuteChanged();

        if (LeakRate <= AlarmLeakRate)
        {
            TestStateText = "OK";
            StateColor = Brushes.LimeGreen;
        }
        else
        {
            TestStateText = "NG (泄漏超标)";
            StateColor = Brushes.Red;
        }
    }

    private bool CanStartTest()
    {
        return !IsTesting;
    }

    // 模拟复位
    [RelayCommand]
    private void Reset()
    {
        IsTesting = false;
        StartTestCommand.NotifyCanExecuteChanged();
        TestStateText = "等待测试";
        StateColor = Brushes.Gray;
        CurrentPressure = 0;
        LeakRate = 0;
    }
}