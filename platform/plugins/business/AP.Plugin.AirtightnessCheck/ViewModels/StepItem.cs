#region

using CommunityToolkit.Mvvm.ComponentModel;

#endregion

namespace AP.Plugin.AirtightnessCheck.ViewModels;

public partial class StepItem : ObservableObject
{
    public int Index { get; set; }
    public int StepNumber => Index + 1;
    public string Header { get; set; }

    public bool IsLastStep { get; set; }

    [ObservableProperty] private bool _isCurrent; // 是否是当前正在进行的步骤

    [ObservableProperty] private bool _isCompleted; // 是否已经完成
}