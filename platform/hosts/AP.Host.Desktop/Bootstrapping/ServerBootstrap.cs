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