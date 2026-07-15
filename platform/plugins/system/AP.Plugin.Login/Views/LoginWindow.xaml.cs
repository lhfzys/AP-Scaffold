using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AP.Plugin.Login.ViewModels;

namespace AP.Plugin.Login.Views;

/// <summary>
/// 登录窗口
/// </summary>
public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel viewModel)
        {
            viewModel.Password = PasswordBox.Password;
        }
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is LoginViewModel viewModel)
        {
            viewModel.LoginCommand.Execute(null);
        }
    }
}
