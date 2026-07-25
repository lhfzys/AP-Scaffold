using MediatR;

namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 统一的设备状态迁移事件（Device Runtime Model）。
/// 任何设备类型（PLC/扫码枪/未来设备）的连接状态迁移都以本事件表达，
/// 消费者按 Info.Type / Info.DeviceId 过滤。
/// 与旧的四个 PLC 连接事件（DeviceConnecting/Connected/ConnectionFailed/Disconnected）并行：
/// 旧事件继续发布（T5.x 迁移完成后再评估退役）。
/// </summary>
public sealed record DeviceStateChangedEvent(
    DeviceInfo Info,
    DeviceConnectionTransition Transition
) : INotification;
