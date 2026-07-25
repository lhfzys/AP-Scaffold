// ============================================================================
// ❄ 封存代码（Frozen）：本文件属于 Server/Client 分布式模式（gRPC）技术栈。
// 当前项目范围仅 Standalone 单机模式：代码保留、不维护、不验证、不投入改进。
// 解冻需专项评审，详见 docs/EVOLUTION_PLAN.md 0.1 节。
// ============================================================================

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