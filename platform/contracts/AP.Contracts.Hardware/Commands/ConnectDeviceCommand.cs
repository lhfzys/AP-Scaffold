using AP.Contracts.Core.Models;
using MediatR;

namespace AP.Contracts.Hardware.Commands;

/// <summary>
/// 手动请求连接设备命令
/// </summary>
public record ConnectDeviceCommand(string DeviceId) : IRequest<OperationResult>;