namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 一次连接/探测尝试的统一结果（协议无关）。
/// 携带失败原因与底层异常，供连接监督器计时、日志与诊断消费；
/// 未来可按 ERROR_HANDLING.md 规范扩展 ErrorCode 等字段而不破坏调用方。
/// </summary>
public sealed class ConnectionAttemptResult
{
    private ConnectionAttemptResult(bool success, string? errorReason, Exception? exception)
    {
        Success = success;
        ErrorReason = errorReason;
        Exception = exception;
    }

    /// <summary>尝试是否成功。</summary>
    public bool Success { get; }

    /// <summary>失败原因（成功时为 null）。</summary>
    public string? ErrorReason { get; }

    /// <summary>底层异常（可选）。</summary>
    public Exception? Exception { get; }

    public static ConnectionAttemptResult Ok() => new(true, null, null);

    public static ConnectionAttemptResult Fail(string reason, Exception? exception = null) => new(false, reason, exception);
}
