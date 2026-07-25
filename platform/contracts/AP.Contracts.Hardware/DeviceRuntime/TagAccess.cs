namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 读写方向。
/// </summary>
public enum TagAccess
{
    /// <summary>只读（如传感器采集值）。</summary>
    ReadOnly = 0,

    /// <summary>只写（如控制输出点）。</summary>
    WriteOnly = 1,

    /// <summary>可读写。</summary>
    ReadWrite = 2,
}
