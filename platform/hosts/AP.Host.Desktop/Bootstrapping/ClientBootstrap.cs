// ============================================================================
// ❄ 封存代码（Frozen）：本文件属于 Server/Client 分布式模式（gRPC）技术栈。
// 当前项目范围仅 Standalone 单机模式：代码保留、不维护、不验证、不投入改进。
// 解冻需专项评审，详见 docs/EVOLUTION_PLAN.md 0.1 节。
// ============================================================================

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