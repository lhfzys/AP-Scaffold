using AP.Host.Desktop.ViewModels;
using System.Windows;

namespace AP.Host.Desktop.Views;

/// <summary>
/// 启动画面窗口
/// </summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
        DataContext = new SplashViewModel();
    }

    public SplashViewModel ViewModel => (SplashViewModel)DataContext;
}
