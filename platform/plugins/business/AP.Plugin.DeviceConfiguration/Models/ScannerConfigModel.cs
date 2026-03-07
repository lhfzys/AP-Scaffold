#region

using System.IO.Ports;

#endregion

namespace AP.Plugin.DeviceConfiguration.Models;

/// <summary>
///     UI专用的扫码枪配置模型
/// </summary>
public class ScannerConfigModel
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;

    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; }

    public Parity Parity { get; set; }
}