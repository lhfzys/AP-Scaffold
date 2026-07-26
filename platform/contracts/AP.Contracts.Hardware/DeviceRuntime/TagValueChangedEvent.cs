using MediatR;

namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 值变化事件（变化才发布：值或质量戳任一变化）。
/// 订阅者如需更多点的当前值，读 LatestTagValueStore 快照而不是打设备；
/// UI 可按 TagValue.Version 做增量刷新。
/// </summary>
public sealed record TagValueChangedEvent(
    string Name,
    TagValue Value
) : INotification;
