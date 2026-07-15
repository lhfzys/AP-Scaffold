using System.Windows;
using AP.Plugin.RoleManagement.ViewModels;

namespace AP.Plugin.RoleManagement.Views;

/// <summary>
/// 角色编辑窗口
/// </summary>
public partial class RoleEditWindow : Window
{
    public RoleEditWindow(RoleEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnViewModelRequestClose;
        Closed += (_, _) => viewModel.RequestClose -= OnViewModelRequestClose;
    }

    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        if (DataContext is RoleEditViewModel vm)
        {
            DialogResult = vm.IsSaved;
        }
        Close();
    }
}
