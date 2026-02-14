using AP.Infra.Grpc.Client;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 客户端策略
/// </summary>
public static class ClientBootstrap
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 注册 gRPC 客户端后台任务
        services.AddHostedService<GrpcClientWorker>();
    }
}