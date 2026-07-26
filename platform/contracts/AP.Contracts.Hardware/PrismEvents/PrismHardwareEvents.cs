#region

using AP.Contracts.Hardware.DeviceRuntime;
using AP.Contracts.Hardware.Events;

#endregion

namespace AP.Contracts.Hardware.PrismEvents;

public class PrismPlcDataChangedEvent : PubSubEvent<PlcDataChangedEvent>
{
}

public class PrismScanCompletedEvent : PubSubEvent<ScanCompletedEvent>
{
}

public class PrismDeviceDisconnectedEvent : PubSubEvent<DeviceDisconnectedEvent>
{
}

/// <summary>
/// Tag 值变化（Prism 桥接通道，UI 订阅；变化才发布）。
/// </summary>
public class PrismTagValueChangedEvent : PubSubEvent<TagValueChangedEvent>
{
}