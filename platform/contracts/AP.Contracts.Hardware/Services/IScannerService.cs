namespace AP.Contracts.Hardware.Services;

/// <summary>
/// 扫码枪接口
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// 打开连接
    /// </summary>
    Task OpenAsync();

    /// <summary>
    /// 关闭连接
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// 是否已连接
    /// </summary>
    bool IsConnected { get; }
}