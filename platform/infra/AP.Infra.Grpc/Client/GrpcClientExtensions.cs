using Microsoft.Extensions.DependencyInjection;

namespace AP.Infra.Grpc.Client;

public static class GrpcClientExtensions
{
    public static IServiceCollection AddPlatformGrpcClient(this IServiceCollection services)
    {
        services.AddSingleton<GrpcChannelFactory>();
        // 后续在这里注册具体的 gRPC Client 服务
        return services;
    }
}