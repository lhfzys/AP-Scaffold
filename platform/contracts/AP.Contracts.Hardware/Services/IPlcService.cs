using AP.Contracts.Hardware.Capabilities;

namespace AP.Contracts.Hardware.Services;

/// <summary>
/// PLC 服务标准接口
/// </summary>
public interface IPlcService
{
    /// <summary>
    /// 获取当前插件支持的特性（如是否支持批量读写、订阅）
    /// </summary>
    PlcServiceFeatures SupportedFeatures { get; }

    /// <summary>
    /// 连接 PLC
    /// </summary>
    Task ConnectAsync(CancellationToken ct = default);

    /// <summary>
    /// 断开 PLC
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// 检查连接状态
    /// </summary>
    Task<bool> IsConnectedAsync();

    /// <summary>
    /// 读取单个数据
    /// </summary>
    /// <typeparam name="T">支持 bool, short, int, float, string 等</typeparam>
    Task<T> ReadAsync<T>(string address, CancellationToken ct = default);

    /// <summary>
    /// 写入单个数据
    /// </summary>
    Task WriteAsync<T>(string address, T value, CancellationToken ct = default);
}

/// <summary>
/// 批量读写扩展接口 (可选特性)
/// </summary>
public interface IPlcBatchReadWrite
{
    Task<Dictionary<string, object>> ReadBatchAsync(string[] addresses, CancellationToken ct = default);
    Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default);
}