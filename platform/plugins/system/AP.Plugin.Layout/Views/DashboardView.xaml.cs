using System.Windows.Controls;
using AP.Plugin.Layout.ViewModels;

namespace AP.Plugin.Layout.Views;

public partial class DashboardView : UserControl
{
    public DashboardView(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}