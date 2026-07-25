namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 数据类型。
/// Bool/Int16/UInt16/Int32/UInt32/Float/String 与现有 PLC 驱动读写族对齐；
/// Int64/UInt64/Double/ByteArray 为非 PLC 数据源（MQTT、仪表等）预留。
/// 驱动实际支持子集由 Tag 层映射，驱动不支持时以 Quality=Bad 表达而非抛异常。
/// </summary>
public enum TagDataType
{
    Bool = 0,
    Int16 = 1,
    UInt16 = 2,
    Int32 = 3,
    UInt32 = 4,
    Int64 = 5,
    UInt64 = 6,
    Float = 7,
    Double = 8,
    String = 9,
    ByteArray = 10,
}
