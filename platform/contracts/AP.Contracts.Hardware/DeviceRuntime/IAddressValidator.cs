namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 地址验证器：把"地址语法是否合法/规范化/解析"的判定收敛在各驱动内部，
/// Infra 层（点表加载等）面向本接口工作，永远见不到协议类型。
/// 每个驱动插件实现一个薄封装（包装其 internal Address Object 解析器）并注册到 DI。
/// </summary>
public interface IAddressValidator
{
    /// <summary>驱动类型标识（与 IPlcDriverFactory.DriverType 一致，如 "Mitsubishi"）。</summary>
    string DriverType { get; }

    /// <summary>
    /// 解析地址。成功时 <paramref name="parsedAddress"/> 为驱动内部的 Address Object
    /// （对调用方不透明，其 ToString() 即规范化表示；仅所属驱动可还原使用）。
    /// </summary>
    bool TryParse(string address, out object? parsedAddress, out string? error);
}
