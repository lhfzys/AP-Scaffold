using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Infra.Security.Audit;
using AP.Infra.Security.Repositories;
using AP.Infra.Security.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Security.Configuration;

/// <summary>
/// 安全模块服务注册扩展
/// </summary>
public static class SecurityServiceExtensions
{
    /// <summary>
    /// 注册单机版身份认证与权限服务
    /// </summary>
    public static IServiceCollection AddPlatformSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var enabled = configuration.GetValue<bool?>("Security:Enabled") ?? true;

        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        if (enabled)
        {
            services.AddSingleton<IUserRepository, UserRepository>();
            services.AddSingleton<IRoleRepository, RoleRepository>();
            services.AddSingleton<IPermissionRepository, PermissionRepository>();
            services.AddSingleton<IIdentityService, IdentityService>();
            services.AddSingleton<ISecurityDbInitializer, Data.SecurityDbInitializer>();
        }
        else
        {
            // 安全模块禁用时注册匿名实现，保证业务插件依赖注入不失败
            services.AddSingleton<IIdentityService, AnonymousIdentityService>();
        }

        // 审计日志不受 Security:Enabled 完全控制，单独判断；默认启用
        var auditEnabled = configuration.GetValue<bool?>("Security:Audit:Enabled") ?? enabled;
        if (auditEnabled)
        {
            services.AddSingleton<IAuditService, AuditService>();
        }
        else
        {
            services.AddSingleton<IAuditService, NullAuditService>();
        }

        return services;
    }
}
