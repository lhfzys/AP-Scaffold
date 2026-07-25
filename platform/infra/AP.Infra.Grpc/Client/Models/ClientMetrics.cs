// ============================================================================
// ❄ 封存代码（Frozen）：本文件属于 Server/Client 分布式模式（gRPC）技术栈。
// 当前项目范围仅 Standalone 单机模式：代码保留、不维护、不验证、不投入改进。
// 解冻需专项评审，详见 docs/EVOLUTION_PLAN.md 0.1 节。
// ============================================================================

namespace AP.Infra.Grpc.Client.Models;

/// <summary>
/// 客户端指标模型
/// </summary>
public class ClientMetrics
{
    public string ClientId { get; set; } = string.Empty;
    public long LastHeartbeatTime { get; set; }
    public bool IsConnected { get; set; }
    public int ReceivedMessageCount { get; set; }
}