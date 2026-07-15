using System.Windows.Controls;
using AP.Plugin.RoleManagement.ViewModels;

namespace AP.Plugin.RoleManagement.Views;

/// <summary>
/// Interaction logic for RoleListView.xaml
/// </summary>
public partial class RoleListView : UserControl
{
    public RoleListView(RoleListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}