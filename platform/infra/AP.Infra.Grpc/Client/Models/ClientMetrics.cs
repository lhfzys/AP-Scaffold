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