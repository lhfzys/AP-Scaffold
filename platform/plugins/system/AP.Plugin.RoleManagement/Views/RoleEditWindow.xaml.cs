using System.Windows;
using AP.Plugin.RoleManagement.ViewModels;

namespace AP.Plugin.RoleManagement.Views;

/// <summary>
/// 角色编辑窗口
/// </summary>
public partial class RoleEditWindow : Window
{
    private RoleEditViewModel? _viewModel;

    public RoleEditWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.RequestClose -= OnViewModelRequestClose;
        }

        if (DataContext is not RoleEditViewModel vm) return;

        _viewModel = vm;
        _viewModel.RequestClose += OnViewModelRequestClose;
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
