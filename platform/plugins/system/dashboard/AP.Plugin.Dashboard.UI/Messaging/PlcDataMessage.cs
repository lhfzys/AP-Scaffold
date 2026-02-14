using AP.Contracts.Hardware.Events;
using CommunityToolkit.Mvvm.Messaging.Messages;

namespace AP.Plugin.Dashboard.UI.Messaging;

/// <summary>
/// 定义一个消息信封，用于在 UI 层内部传递 PLC 数据
/// </summary>
public class PlcDataMessage : ValueChangedMessage<PlcDataChangedEvent>
{
    public PlcDataMessage(PlcDataChangedEvent value) : base(value)
    {
    }
}