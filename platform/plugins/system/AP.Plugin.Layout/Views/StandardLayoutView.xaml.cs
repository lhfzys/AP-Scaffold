using System.Windows.Controls;
using AP.Plugin.Layout.ViewModels;

namespace AP.Plugin.Layout.Views;

public partial class StandardLayoutView : UserControl
{
    public StandardLayoutView(SidebarView sidebarView, SidebarViewModel sidebarViewModel, LayoutViewModel layoutViewModel)
    {
        InitializeComponent();
        DataContext = layoutViewModel;
        sidebarView.DataContext = sidebarViewModel;
        SidebarContainer.Content = sidebarView;
    }
}
