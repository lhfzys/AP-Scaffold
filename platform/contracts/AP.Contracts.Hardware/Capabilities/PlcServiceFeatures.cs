namespace AP.Contracts.Hardware.Capabilities;

[Flags]
public enum PlcServiceFeatures
{
    None = 0,
    BasicReadWrite = 1 << 0, // 基础读写
    BatchReadWrite = 1 << 1, // 批量读写
    Subscribe = 1 << 2, // 数据订阅
    HotSwitch = 1 << 3, // 热切换
    AutoReconnect = 1 << 4 // 自动重连
}