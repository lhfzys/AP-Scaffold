#region

using System.Collections.ObjectModel;
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
    public ObservableCollection<StepItem> Steps { get; }

    [ObservableProperty] private int _currentIndex = 0; // 当前步进索引

    public AirtightnessViewModel(IOptions<AirtightnessOptions> options)
    {
        _options = options.Value;

        Steps =
        [
            new StepItem { Index = 0, Header = "扫激光码", IsLastStep = false },
            new StepItem { Index = 1, Header = "检具夹紧", IsLastStep = false },
            new StepItem { Index = 2, Header = "气密检测", IsLastStep = false },
            new StepItem { Index = 3, Header = "打开扫签码", IsLastStep = true }
        ];
        // 初始化状态
        UpdateStepStates();
    }

    [RelayCommand(CanExecute = nameof(CanGoNext))]
    private void NextStep()
    {
        if (CurrentIndex < Steps.Count - 1)
        {
            CurrentIndex++;
            UpdateStepStates();
            NextStepCommand.NotifyCanExecuteChanged();
            PrevStepCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGoNext()
    {
        return CurrentIndex < Steps.Count - 1;
    }

    // 🟢 上一步命令
    [RelayCommand(CanExecute = nameof(CanGoPrev))]
    private void PrevStep()
    {
        if (CurrentIndex > 0)
        {
            CurrentIndex--;
            UpdateStepStates();
            NextStepCommand.NotifyCanExecuteChanged();
            PrevStepCommand.NotifyCanExecuteChanged();
        }
    }

    private bool CanGoPrev()
    {
        return CurrentIndex > 0;
    }

    private void UpdateStepStates()
    {
        foreach (var step in Steps)
        {
            step.IsCurrent = step.Index == CurrentIndex;
            step.IsCompleted = step.Index < CurrentIndex;
        }
    }
}