namespace AP.Plugin.Scanner.Configuration;

/// <summary>
/// 扫码枪配置
/// </summary>
public class SerialPortOptions
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Scanner";

    /// <summary>是否启用扫码枪（false 时不注册服务/设备、不发起连接，用于无扫码枪的项目）。</summary>
    public bool Enabled { get; set; } = true;

    public string PortName { get; set; } = "COM10";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string NewLine { get; set; } = "\r";

    /// <summary>扫码数据后台通道容量；超过容量时丢弃新扫码并记警告，避免串口事件线程无限堆积。</summary>
    public int BarcodeChannelCapacity { get; set; } = 1000;

    /// <summary>串口 ReadLine 超时（毫秒）；避免无换行数据长期占用 DataReceived 线程。</summary>
    public int ReadTimeoutMilliseconds { get; set; } = 1000;
}