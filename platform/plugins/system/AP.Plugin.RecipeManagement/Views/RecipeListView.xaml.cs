using System.Windows.Controls;
using AP.Plugin.RecipeManagement.ViewModels;

namespace AP.Plugin.RecipeManagement.Views;

/// <summary>
/// 配方列表视图
/// </summary>
public partial class RecipeListView : UserControl
{
    public RecipeListView(RecipeListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
