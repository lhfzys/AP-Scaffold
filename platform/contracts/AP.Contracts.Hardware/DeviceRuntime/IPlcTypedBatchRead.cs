namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 带类型的批量读取能力（可选特性）。
/// 取代旧 <see cref="AP.Contracts.Hardware.Services.IPlcBatchReadWrite"/> 的"全按 Int16"语义：
/// 每个地址携带自己的数据类型，驱动按类型正确读取（西门子/欧姆龙为真批量，三菱为循环单点，
/// 对调用方透明）。整批失败抛异常，由调用方决定降级策略（采集引擎降级为逐点读）。
/// 驱动不支持本接口时，经 ActivePlcService 调用抛 <see cref="NotSupportedException"/>。
/// </summary>
public interface IPlcTypedBatchRead
{
    /// <summary>批量读取，按地址返回值（装箱）。</summary>
    Task<Dictionary<string, object>> ReadBatchAsync(IReadOnlyList<BatchReadItem> items, CancellationToken ct = default);
}
