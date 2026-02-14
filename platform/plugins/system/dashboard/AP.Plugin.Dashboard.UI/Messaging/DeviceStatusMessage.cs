using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AP.Plugin.Dashboard.UI.Messaging;

/// <summary>
/// 设备连接状态消息 (用于 UI 广播)
/// </summary>
public class DeviceStatusMessage : ValueChangedMessage<bool>
{
    public string StatusText { get; }
    public Brush StatusColor { get; }

    public DeviceStatusMessage(bool isConnected, string text, Brush color) : base(isConnected)
    {
        StatusText = text;
        StatusColor = color;
    }
}