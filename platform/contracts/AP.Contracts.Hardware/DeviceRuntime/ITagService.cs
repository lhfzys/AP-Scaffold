namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 服务：业务层按逻辑点名读写设备数据的唯一入口。
/// 通信失败是常态——读写失败返回 Quality=Bad 的 <see cref="TagValue"/>（含原因），不抛异常；
/// 仅编程错误抛异常（点名不存在 <see cref="ArgumentException"/>、读写方向违规 <see cref="InvalidOperationException"/>）。
/// </summary>
public interface ITagService
{
    /// <summary>按点名读取，返回带质量戳/时间戳的值。</summary>
    Task<TagValue> ReadAsync(string name, CancellationToken ct = default);

    /// <summary>按点名写入，成功返回 Good(value)，失败返回 Bad(原因)。</summary>
    Task<TagValue> WriteAsync(string name, object? value, CancellationToken ct = default);
}
