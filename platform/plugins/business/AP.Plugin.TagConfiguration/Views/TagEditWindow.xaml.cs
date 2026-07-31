using System.Windows;
using AP.Plugin.TagConfiguration.ViewModels;

namespace AP.Plugin.TagConfiguration.Views;

/// <summary>
/// 点编辑窗口（新增/编辑共用）
/// </summary>
public partial class TagEditWindow : Window
{
    public TagEditWindow(TagEditDialogViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnViewModelRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnViewModelRequestClose;
    }

    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        if (DataContext is TagEditDialogViewModel vm)
        {
            DialogResult = vm.IsSaved;
        }
        Close();
    }
}
