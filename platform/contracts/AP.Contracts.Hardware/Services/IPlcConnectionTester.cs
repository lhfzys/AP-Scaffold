using AP.Contracts.Hardware.Models;

namespace AP.Contracts.Hardware.Services;

/// <summary>PLC 连接测试结果（不修改运行中的 PLC 连接）。</summary>
public sealed record PlcConnectionTestResult(
    bool Connected,
    bool ReadOk,
    string Message,
    long ElapsedMilliseconds,
    string? ReadValue = null)
{
    public static PlcConnectionTestResult Fail(string message, long elapsedMilliseconds)
        => new(false, false, message, elapsedMilliseconds);
}

/// <summary>
/// PLC 连接测试：使用当前编辑中的临时配置创建独立驱动，连接/读取后立即释放，
/// 不影响正在运行的 ActivePlcService。
/// </summary>
public interface IPlcConnectionTester
{
    Task<PlcConnectionTestResult> TestAsync(PlcOptions options, CancellationToken ct = default);
}
