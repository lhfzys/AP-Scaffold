namespace AP.Contracts.Core.Errors;

/// <summary>
/// 常量定义错误码
/// </summary>
public static class ErrorCode
{
    public const string None = "None";
    public const string SystemError = "SYSTEM_ERROR";
    public const string InvalidParameter = "INVALID_PARAMETER";
    public const string NotFound = "NOT_FOUND";
    public const string Timeout = "TIMEOUT";
    public const string Unauthorized = "UNAUTHORIZED";

    // 硬件相关
    public const string DeviceNotConnected = "DEVICE_NOT_CONNECTED";
    public const string DeviceReadFailed = "DEVICE_READ_FAILED";
    public const string DeviceWriteFailed = "DEVICE_WRITE_FAILED";
}