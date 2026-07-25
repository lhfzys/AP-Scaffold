using AP.Contracts.Core.Errors;

namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备/点表配置错误异常（启动校验失败，快速失败策略）。
/// </summary>
public class DeviceConfigurationException : PlatformException
{
    public DeviceConfigurationException(string message)
        : base(message, AP.Contracts.Core.Errors.ErrorCode.ConfigInvalid)
    {
    }
}
