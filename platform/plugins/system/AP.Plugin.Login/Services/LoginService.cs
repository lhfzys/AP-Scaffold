using AP.Contracts.System.Services;
using AP.Plugin.Login.ViewModels;
using AP.Plugin.Login.Views;
using AP.Shared.UI.Base;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.Login.Services;

/// <summary>
/// 登录对话框服务实现
/// </summary>
public class LoginService : ILoginService
{
    private readonly IServiceProvider _serviceProvider;

    public LoginService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public bool ShowLoginDialog()
    {
        var window = _serviceProvider.GetRequiredService<LoginWindow>();
        var viewModel = _serviceProvider.GetRequiredService<LoginViewModel>();
        window.DataContext = viewModel;

        SetWindowOwner(window);
        SubscribeClose(viewModel, window);

        window.ShowDialog();
        return viewModel.IsAuthenticated;
    }

    public bool ShowChangePasswordDialog(string userName)
    {
        var window = _serviceProvider.GetRequiredService<ChangePasswordWindow>();
        var viewModel = _serviceProvider.GetRequiredService<ChangePasswordViewModel>();
        viewModel.UserName = userName;
        window.DataContext = viewModel;

        SetWindowOwner(window);
        SubscribeClose(viewModel, window);

        window.ShowDialog();
        return viewModel.IsChanged;
    }

    private static void SetWindowOwner(System.Windows.Window window)
    {
        var owner = System.Windows.Application.Current.MainWindow;
        if (owner != null && owner.IsVisible)
        {
            window.Owner = owner;
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;
        }
        else
        {
            window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
        }
    }

    private static void SubscribeClose(ViewModelBase viewModel, System.Windows.Window window)
    {
        EventHandler? closeHandler = null;
        closeHandler = (s, e) => window.Close();
        viewModel.RequestClose += closeHandler;
        window.Closed += (s, e) => viewModel.RequestClose -= closeHandler;
    }
}
