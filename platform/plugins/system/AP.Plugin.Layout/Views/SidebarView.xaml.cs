using System.Windows.Controls;
using AP.Plugin.Layout.ViewModels;

namespace AP.Plugin.Layout.Views;

/// <summary>
/// Interaction logic for SidebarView.xaml
/// </summary>
public partial class SidebarView : UserControl
{
    public SidebarView(SidebarViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}