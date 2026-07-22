using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Services;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AP.Infra.Hardware.Services;

/// <summary>
/// PLC 写操作审计装饰器（拦截器）。
/// 包装 <see cref="ActivePlcService"/>，对 WriteAsync / WriteBatchAsync 自动留痕，
/// 业务代码无感知；读操作与连接管理不审计。
/// 审计/身份服务未注册时自动降级为不审计，审计失败不影响写入本身。
/// </summary>
public class AuditingPlcServiceDecorator : IPlcService, IPlcBatchReadWrite
{
    private readonly IPlcService _inner;
    private readonly IAuditService? _auditService;
    private readonly IIdentityService? _identityService;
    private readonly ILogger<AuditingPlcServiceDecorator> _logger;

    public AuditingPlcServiceDecorator(
        IPlcService inner,
        IAuditService? auditService = null,
        IIdentityService? identityService = null,
        ILogger<AuditingPlcServiceDecorator>? logger = null)
    {
        _inner = inner;
        _auditService = auditService;
        _identityService = identityService;
        _logger = logger ?? NullLogger<AuditingPlcServiceDecorator>.Instance;
    }

    public PlcServiceFeatures SupportedFeatures => _inner.SupportedFeatures;

    public Task ConnectAsync(CancellationToken ct = default) => _inner.ConnectAsync(ct);

    public Task DisconnectAsync() => _inner.DisconnectAsync();

    public Task<bool> IsConnectedAsync() => _inner.IsConnectedAsync();

    public Task<T> ReadAsync<T>(string address, CancellationToken ct = default)
        => _inner.ReadAsync<T>(address, ct);

    public async Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
    {
        string? error = null;
        try
        {
            await _inner.WriteAsync(address, value, ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            await LogAuditSafeAsync("PLC 写入", address, $"写入 {address} = {value}", error);
        }
    }

    public Task<Dictionary<string, object>> ReadBatchAsync(string[] addresses, CancellationToken ct = default)
        => _inner is IPlcBatchReadWrite batch
            ? batch.ReadBatchAsync(addresses, ct)
            : throw new NotSupportedException("当前 PLC 驱动不支持批量读取");

    public async Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default)
    {
        if (_inner is not IPlcBatchReadWrite batch)
            throw new NotSupportedException("当前 PLC 驱动不支持批量写入");

        string? error = null;
        try
        {
            await batch.WriteBatchAsync(data, ct);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            throw;
        }
        finally
        {
            var targets = string.Join(",", data.Keys);
            await LogAuditSafeAsync("PLC 批量写入", targets, $"批量写入 {data.Count} 个地址: {targets}", error);
        }
    }

    private async Task LogAuditSafeAsync(string actionName, string targetId, string description, string? error)
    {
        if (_auditService is null) return;

        try
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                // 未登录（后台任务/启动期）记为 system；Security 禁用时 CurrentUser 恒为 anonymous
                UserName = _identityService?.CurrentUser?.UserName ?? "system",
                ActionType = AuditActionType.ManualControl,
                ActionName = actionName,
                TargetId = targetId,
                Description = description,
                Succeeded = error is null,
                ErrorMessage = error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录 PLC 写操作审计日志失败");
        }
    }
}
