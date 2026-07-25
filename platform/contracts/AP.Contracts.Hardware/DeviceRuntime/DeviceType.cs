namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 设备类型（粗粒度分类，刻意保持少量取值）。
/// 扩展策略：新设备种类先落 <see cref="Other"/>，细分信息由 DeviceInfo.DriverType 承载；
/// 确有规模与差异化行为时才新增枚举值（新增枚举成员向后兼容），避免契约层随设备种类频繁改版。
/// </summary>
public enum DeviceType
{
    /// <summary>可编程逻辑控制器（各品牌经 DriverType 区分）。</summary>
    Plc = 0,

    /// <summary>扫码枪等串口输入设备。</summary>
    Scanner = 1,

    /// <summary>其他/暂未分类设备（相机、机器人、MQTT 等预留落点）。</summary>
    Other = 2,
}
