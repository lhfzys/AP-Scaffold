using System.Windows;
using System.Windows.Controls;
using AP.Plugin.UserManagement.ViewModels;

namespace AP.Plugin.UserManagement.Views;

/// <summary>
/// 用户编辑窗口
/// </summary>
public partial class UserEditWindow : Window
{
    public UserEditWindow(UserEditViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.RequestClose += OnViewModelRequestClose;
        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) =>
        {
            viewModel.RequestClose -= OnViewModelRequestClose;
            viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        };

        SyncSelectedRoles();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UserEditViewModel.SelectedRoles))
        {
            SyncSelectedRoles();
        }
    }

    private void SyncSelectedRoles()
    {
        if (DataContext is not UserEditViewModel vm) return;

        RolesListBox.SelectedItems.Clear();
        foreach (var role in vm.SelectedRoles)
        {
            RolesListBox.SelectedItems.Add(role);
        }
    }

    private void OnViewModelRequestClose(object? sender, EventArgs e)
    {
        if (DataContext is UserEditViewModel vm)
        {
            DialogResult = vm.IsSaved;
        }
        Close();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }

    private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is UserEditViewModel viewModel)
        {
            viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
        }
    }

    private void RolesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not UserEditViewModel vm) return;

        foreach (var added in e.AddedItems.Cast<string>())
        {
            if (!vm.SelectedRoles.Contains(added))
                vm.SelectedRoles.Add(added);
        }

        foreach (var removed in e.RemovedItems.Cast<string>())
        {
            vm.SelectedRoles.Remove(removed);
        }
    }
}
