namespace AP.Contracts.Core.Models;

/// <summary>
/// 通用操作结果封装
/// </summary>
public class OperationResult<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public static OperationResult<T> Ok(T data, string message = "Success")
    {
        return new OperationResult<T> { Success = true, Data = data, Message = message };
    }

    public static OperationResult<T> Fail(string message, string errorCode = "UNKNOWN_ERROR")
    {
        return new OperationResult<T> { Success = false, Message = message, ErrorCode = errorCode };
    }
}

/// <summary>
/// 无返回值的操作结果
/// </summary>
public class OperationResult : OperationResult<object?>
{
    public static OperationResult Ok(string message = "Success")
    {
        return new OperationResult { Success = true, Data = null, Message = message };
    }

    public new static OperationResult Fail(string message, string errorCode = "UNKNOWN_ERROR")
    {
        return new OperationResult { Success = false, Message = message, ErrorCode = errorCode };
    }
}