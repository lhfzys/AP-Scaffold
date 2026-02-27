#region

using CommunityToolkit.Mvvm.ComponentModel;

#endregion

namespace AP.Plugin.AirtightnessCheck.ViewModels.Steps;

// 第一步：扫码
public partial class ScanStepViewModel : ObservableObject
{
    [ObservableProperty] private string _barcode = "";
}

// 第二步：测试运行
public partial class TestStepViewModel : ObservableObject
{
    [ObservableProperty] private double _currentPressure = 0.0;
    // 放入您之前的测试逻辑...
}

// 第三步：结果展示
public partial class ResultStepViewModel : ObservableObject
{
    [ObservableProperty] private string _resultText = "合格";
}