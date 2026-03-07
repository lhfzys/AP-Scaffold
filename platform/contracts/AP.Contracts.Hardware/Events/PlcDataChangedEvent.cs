#region

using AP.Contracts.Hardware.Models;
using MediatR;

#endregion

namespace AP.Contracts.Hardware.Events;

/// <summary>
///     PLC 数据变化事件 (用于 MediatR 发布)
/// </summary>
/// <param name="DeviceName">设备名称 (如 "Mitsubishi-Q")</param>
/// <param name="Address">地址/标签 (如 "D100")</param>
/// <param name="Value">新值</param>
/// <param name="Timestamp">时间戳</param>
public record PlcDataChangedEvent(
    string DeviceName,
    string Address,
    PlcValue Value,
    DateTime Timestamp
) : INotification;

/// <summary>
///     批量事件发送补充
/// </summary>
/// <param name="DeviceName"></param>
/// <param name="Changes"></param>
/// <param name="Timestamp"></param>
public record PlcBatchDataChangedEvent(
    string DeviceName,
    IReadOnlyList<(string Address, PlcValue Value)> Changes, // 批量传递
    DateTime Timestamp
) : INotification;