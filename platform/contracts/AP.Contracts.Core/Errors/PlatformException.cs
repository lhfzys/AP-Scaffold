namespace AP.Contracts.Core.Errors;

/// <summary>
/// 异常基类
/// </summary>
public class PlatformException : Exception
{
    public string ErrorCode { get; }

    public PlatformException(string message, string errorCode = Errors.ErrorCode.SystemError)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public PlatformException(string message, Exception innerException, string errorCode = Errors.ErrorCode.SystemError)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}