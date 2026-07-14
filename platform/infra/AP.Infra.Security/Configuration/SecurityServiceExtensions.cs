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
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<IUserRepository, UserRepository>();
        services.AddSingleton<IIdentityService, IdentityService>();
        services.AddSingleton<IAuditService, AuditService>();
        services.AddSingleton<AP.Contracts.Security.Abstractions.ISecurityDbInitializer, Data.SecurityDbInitializer>();

        return services;
    }
}
