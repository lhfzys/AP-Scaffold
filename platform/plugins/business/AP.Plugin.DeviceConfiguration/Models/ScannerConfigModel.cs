using System.IO.Ports;

namespace AP.Plugin.DeviceConfiguration.Models;

/// <summary>
/// UI 专用的扫码枪配置模型
/// 对应 appsettings.json 中 Plugins:Configuration:AP.Plugin.Scanner 节点
/// </summary>
public class ScannerConfigModel
{
    public const string SectionName = "Plugins:Configuration:AP.Plugin.Scanner";

    /// <summary>是否启用扫码枪（false 时重启后不再连接该设备）。</summary>
    public bool Enabled { get; set; } = true;

    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public Parity Parity { get; set; } = Parity.None;
    public StopBits StopBits { get; set; } = StopBits.One;
    public string NewLine { get; set; } = "\r";
}
