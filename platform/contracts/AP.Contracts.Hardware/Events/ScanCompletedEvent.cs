using MediatR;

namespace AP.Contracts.Hardware.Events;

/// <summary>
/// 扫码完成事件
/// </summary>
/// <param name="MachineId">机器/工位唯一ID (如 "Station-01")</param>
/// <param name="DeviceName">硬件设备源 (如 "COM3" 或 "Scanner-Left")</param>
/// <param name="Barcode">条码内容</param>
/// <param name="Timestamp">扫描时间</param>
public record ScanCompletedEvent(
    string MachineId, // 👈 新增：这是给服务器看的
    string DeviceName, // 👈 原SourceDevice：这是给本地排错看的
    string Barcode,
    DateTime Timestamp
) : INotification;