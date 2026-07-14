using AP.Contracts.System.Services;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Login.Services;
using AP.Plugin.Login.ViewModels;
using AP.Plugin.Login.Views;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.Login;

/// <summary>
/// 登录插件
/// </summary>
[PluginMetadata("AP.Plugin.Login", Name = "登录认证", Version = "1.0.0", Priority = 1)]
public class LoginPlugin : PluginBase
{
    public LoginPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册登录服务
        services.AddSingleton<ILoginService, LoginService>();

        // 注册登录与改密窗口
        services.AddTransient<LoginWindow>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<ChangePasswordWindow>();
        services.AddTransient<ChangePasswordViewModel>();
    }
}
