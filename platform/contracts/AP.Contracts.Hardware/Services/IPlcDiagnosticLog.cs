namespace AP.Contracts.Hardware.Services;

/// <summary>一条 PLC 通讯日志（应用层请求/响应，不是网卡抓包级别的原始十六进制帧）。</summary>
public sealed record PlcDiagnosticEntry(
    DateTime Timestamp,
    string Operation,
    string Address,
    string? Value,
    long ElapsedMilliseconds,
    bool Success,
    string? Error);

/// <summary>
/// PLC 通讯日志：内存环形缓冲，供现场诊断页查看最近操作。
/// 只保留最近若干条，不写数据库；完整审计仍由 Audit 服务负责。
/// </summary>
public interface IPlcDiagnosticLog
{
    event EventHandler? Changed;

    void Record(PlcDiagnosticEntry entry);

    /// <summary>最新在前。</summary>
    IReadOnlyList<PlcDiagnosticEntry> GetRecent(int max = 200);

    void Clear();
}
