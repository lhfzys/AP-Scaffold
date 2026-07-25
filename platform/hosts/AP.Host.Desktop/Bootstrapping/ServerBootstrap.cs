// ============================================================================
// ❄ 封存代码（Frozen）：本文件属于 Server/Client 分布式模式（gRPC）技术栈。
// 当前项目范围仅 Standalone 单机模式：代码保留、不维护、不验证、不投入改进。
// 解冻需专项评审，详见 docs/EVOLUTION_PLAN.md 0.1 节。
// ============================================================================

using AP.Contracts.Hardware.Events;
using AP.Infra.Grpc.Server;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Host.Desktop.Bootstrapping;

/// <summary>
/// 服务端策略
/// </summary>
public static class ServerBootstrap
{
    public static void RegisterServices(IServiceCollection services)
    {
        // 注册 gRPC 数据广播器
        services.AddSingleton<StreamBroadcaster>();

        // 将广播器注册为 MediatR 事件处理器 (当 PLC 数据变化时触发广播)
        services.AddTransient<INotificationHandler<PlcDataChangedEvent>>(sp =>
            sp.GetRequiredService<StreamBroadcaster>());
    }

    public static void OnInitialized(IServiceProvider provider)
    {
        // Server 端特有的初始化逻辑 (如果有)
    }
}