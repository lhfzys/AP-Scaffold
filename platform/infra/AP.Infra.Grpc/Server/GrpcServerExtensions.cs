using AP.Infra.Grpc.Server.Interceptors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Grpc.Server;

/// <summary>
/// Server 模式启动时调用的扩展方法，负责配置 Kestrel 和注册 gRPC 服务
/// </summary>
public static class GrpcServerExtensions
{
    /// <summary>
    /// 添加 gRPC 服务端支持 (Kestrel 配置)
    /// </summary>
    public static IServiceCollection AddPlatformGrpcServer(this IServiceCollection services,
        IConfiguration configuration)
    {
        // 注册广播器 (单例)
        services.AddSingleton<StreamBroadcaster>();
        // 注册 gRPC 核心服务
        services.AddGrpc(options =>
        {
            options.EnableDetailedErrors = true; // 开发环境开启
            options.Interceptors.Add<LoggingInterceptor>();
        });

        services.AddSingleton<LoggingInterceptor>();

        return services;
    }

    /// <summary>
    /// 配置 Kestrel 监听端口
    /// </summary>
    public static void ConfigureKestrelForGrpc(this WebApplicationBuilder builder, int port)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            // gRPC 需要 HTTP/2
            options.ListenAnyIP(port, listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; });
        });
    }
}