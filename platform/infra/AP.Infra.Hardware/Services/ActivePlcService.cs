using AP.Contracts.Hardware.Capabilities;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AP.Infra.Hardware.Services;

/// <summary>
/// 当前激活的 PLC 服务代理。
/// 根据 <see cref="PlcOptions.DriverType"/> 从 <see cref="PlcDriverRegistry"/> 中选择真实驱动，
/// 并转发所有 <see cref="IPlcService"/> / <see cref="IPlcBatchReadWrite"/> 调用。
/// </summary>
public class ActivePlcService : IPlcService, IPlcBatchReadWrite
{
    private readonly Lazy<IPlcService> _innerLazy;

    public ActivePlcService(
        IOptions<PlcOptions> options,
        PlcDriverRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<ActivePlcService> logger)
    {
        _innerLazy = new Lazy<IPlcService>(() =>
        {
            var opt = options.Value;
            logger.LogInformation(
                "正在激活 PLC 驱动: {DriverType} ({IpAddress}:{Port})",
                opt.DriverType, opt.IpAddress, opt.Port);

            var factory = registry.GetFactory(opt.DriverType);
            return factory.CreateDriver(opt, serviceProvider);
        }, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    private IPlcService Inner => _innerLazy.Value;

    public PlcServiceFeatures SupportedFeatures => Inner.SupportedFeatures;

    public Task ConnectAsync(CancellationToken ct = default) => Inner.ConnectAsync(ct);

    public Task DisconnectAsync() => Inner.DisconnectAsync();

    public Task<bool> IsConnectedAsync() => Inner.IsConnectedAsync();

    public Task<T> ReadAsync<T>(string address, CancellationToken ct = default)
        => Inner.ReadAsync<T>(address, ct);

    public Task WriteAsync<T>(string address, T value, CancellationToken ct = default)
        => Inner.WriteAsync(address, value, ct);

    public Task<Dictionary<string, object>> ReadBatchAsync(string[] addresses, CancellationToken ct = default)
        => Inner is IPlcBatchReadWrite batch
            ? batch.ReadBatchAsync(addresses, ct)
            : throw new NotSupportedException("当前 PLC 驱动不支持批量读取");

    public Task WriteBatchAsync(Dictionary<string, object> data, CancellationToken ct = default)
        => Inner is IPlcBatchReadWrite batch
            ? batch.WriteBatchAsync(data, ct)
            : throw new NotSupportedException("当前 PLC 驱动不支持批量写入");
}
