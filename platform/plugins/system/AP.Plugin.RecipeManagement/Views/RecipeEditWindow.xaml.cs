using System.Windows;
using AP.Plugin.RecipeManagement.ViewModels;

namespace AP.Plugin.RecipeManagement.Views;

/// <summary>
/// 配方编辑窗口
/// </summary>
public partial class RecipeEditWindow : Window
{
    public RecipeEditWindow(RecipeEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnViewModelRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnViewModelRequestClose;
    }

    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        if (DataContext is RecipeEditViewModel vm)
        {
            DialogResult = vm.IsSaved;
        }
        Close();
    }
}
