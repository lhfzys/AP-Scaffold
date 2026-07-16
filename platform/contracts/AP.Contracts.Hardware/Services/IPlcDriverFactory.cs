using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;

namespace AP.Contracts.Hardware.Services;

/// <summary>
/// PLC 驱动工厂抽象。
/// 每个 PLC 品牌插件实现一个工厂，由驱动注册表统一调度。
/// </summary>
public interface IPlcDriverFactory
{
    /// <summary>
    /// 驱动类型标识，例如 "Mitsubishi" / "Siemens" / "Omron"。
    /// </summary>
    string DriverType { get; }

    /// <summary>
    /// 该驱动支持的特性。
    /// </summary>
    PlcServiceFeatures SupportedFeatures { get; }

    /// <summary>
    /// 创建真实的 PLC 服务实例。
    /// </summary>
    /// <param name="options">PLC 连接配置</param>
    /// <param name="serviceProvider">服务提供程序，用于解析 Polly 策略、MediatR、日志等</param>
    /// <returns>PLC 服务实例</returns>
    IPlcService CreateDriver(PlcOptions options, IServiceProvider serviceProvider);
}
