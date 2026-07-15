using System.Windows.Controls;
using AP.Plugin.UserManagement.ViewModels;

namespace AP.Plugin.UserManagement.Views;

/// <summary>
/// 用户列表视图
/// </summary>
public partial class UserListView : UserControl
{
    public UserListView(UserListViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
