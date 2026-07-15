using System.Windows.Controls;
using AP.Plugin.Layout.ViewModels;

namespace AP.Plugin.Layout.Views;

public partial class StandardLayoutView : UserControl
{
    public StandardLayoutView(SidebarView sidebarView, SidebarViewModel viewModel)
    {
        InitializeComponent();
        sidebarView.DataContext = viewModel;
        SidebarContainer.Content = sidebarView;
    }
}
