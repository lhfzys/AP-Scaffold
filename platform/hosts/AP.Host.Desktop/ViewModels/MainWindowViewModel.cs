using AP.Contracts.Core.Events;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AP.Host.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLoading = true;

    public MainWindowViewModel(IEventAggregator eventAggregator)
    {
        eventAggregator.GetEvent<AppInitializedEvent>().Subscribe(OnInitialized, ThreadOption.UIThread);
    }

    private void OnInitialized()
    {
        IsLoading = false;
    }
}