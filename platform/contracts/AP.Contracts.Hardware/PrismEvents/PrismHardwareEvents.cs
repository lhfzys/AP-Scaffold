#region

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