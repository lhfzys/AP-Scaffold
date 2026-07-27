namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 带类型的批量读取项（地址 + 数据类型）。
/// </summary>
public sealed record BatchReadItem(string Address, TagDataType DataType);
