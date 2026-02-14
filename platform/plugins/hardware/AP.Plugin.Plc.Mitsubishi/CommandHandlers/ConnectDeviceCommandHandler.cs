using AP.Contracts.Core.Models;
using AP.Contracts.Hardware.Commands;
using AP.Contracts.Hardware.Services;
using MediatR;

namespace AP.Plugin.Plc.Mitsubishi.CommandHandlers;

/// <summary>
/// 专门处理手动连接命令的处理器
/// </summary>
public class ConnectDeviceCommandHandler : IRequestHandler<ConnectDeviceCommand, OperationResult>
{
    private readonly IPlcService _plcService;

    public ConnectDeviceCommandHandler(IPlcService plcService)
    {
        _plcService = plcService;
    }

    public async Task<OperationResult> Handle(ConnectDeviceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // 调用 Service 执行连接
            await _plcService.ConnectAsync(cancellationToken);
            return OperationResult.Ok();
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }
}