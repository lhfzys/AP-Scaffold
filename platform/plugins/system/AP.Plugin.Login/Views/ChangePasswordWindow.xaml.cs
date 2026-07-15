using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AP.Plugin.Login.ViewModels;

namespace AP.Plugin.Login.Views;

/// <summary>
/// 修改密码窗口
/// </summary>
public partial class ChangePasswordWindow : Window
{
    public ChangePasswordWindow()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ChangePasswordViewModel viewModel) return;

        if (sender == CurrentPasswordBox)
            viewModel.CurrentPassword = CurrentPasswordBox.Password;
        else if (sender == NewPasswordBox)
            viewModel.NewPassword = NewPasswordBox.Password;
        else if (sender == ConfirmPasswordBox)
            viewModel.ConfirmPassword = ConfirmPasswordBox.Password;
    }

    private void ConfirmPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChangePasswordViewModel viewModel)
        {
            viewModel.ChangePasswordCommand.Execute(null);
        }
    }
}
