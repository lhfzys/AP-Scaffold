using AP.Contracts.System.Services;
using AP.Core.Enums;
using AP.Core.PluginFramework.Attributes;
using AP.Plugin.Dashboard.Core.Services;
using AP.Shared.PluginSDK.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.Dashboard.Core;

[PluginMetadata(
    "AP.Plugin.Dashboard.Core",
    Name = "系统监控核心服务",
    Version = "1.0.0",
    SupportedRoles = AppRole.All,
    Priority = 10
)]
public class DashboardPlugin : PluginBase
{
    public DashboardPlugin(ILogger logger) : base(logger)
    {
    }

    public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        base.ConfigureServices(services, configuration);

        // 注册监控服务供 UI 层调用
        services.AddSingleton<ISystemMonitorService, SystemMonitorService>();
    }
}