using System.Diagnostics;
using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Hardware.Services;

/// <summary>通过临时驱动实例测试 PLC 连接与心跳地址读取，不触碰正在运行的服务。</summary>
public sealed class PlcConnectionTester : IPlcConnectionTester
{
    private readonly PlcDriverRegistry _registry;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PlcConnectionTester> _logger;

    public PlcConnectionTester(
        PlcDriverRegistry registry,
        IServiceProvider serviceProvider,
        ILogger<PlcConnectionTester> logger)
    {
        _registry = registry;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<PlcConnectionTestResult> TestAsync(PlcOptions options, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var timeout = TimeSpan.FromMilliseconds(Math.Clamp(options.Timeout, 500, 15000));
        IPlcService? driver = null;

        try
        {
            var factory = _registry.GetFactory(options.DriverType);
            driver = factory.CreateDriver(options, _serviceProvider);

            await driver.ConnectAsync(ct).WaitAsync(timeout, ct);

            // 驱动的 ConnectAsync 可能只启动连接监督器并立即返回（如 Siemens），
            // 必须轮询到真实 Connected 状态后再读心跳，否则会误报“连接成功但读取失败”。
            while (!await driver.IsConnectedAsync().WaitAsync(timeout, ct))
            {
                if (stopwatch.Elapsed >= timeout)
                    return PlcConnectionTestResult.Fail(
                        $"连接超时（{timeout.TotalMilliseconds:F0} ms）：{options.IpAddress}:{options.Port}",
                        stopwatch.ElapsedMilliseconds);

                await Task.Delay(100, ct);
            }

            stopwatch.Stop();

            // 连接测试后再读一次心跳地址：虚拟 PLC / 现场 PLC 都要能读，才算真正可用
            try
            {
                string readValue;
                if (string.Equals(options.DriverType, "Omron", StringComparison.OrdinalIgnoreCase))
                    readValue = (await driver.ReadAsync<short>(options.HeartbeatAddress, ct)
                        .WaitAsync(timeout, ct)).ToString();
                else
                    readValue = (await driver.ReadAsync<bool>(options.HeartbeatAddress, ct)
                        .WaitAsync(timeout, ct)).ToString();

                return new PlcConnectionTestResult(
                    true,
                    true,
                    $"连接成功，读取 {options.HeartbeatAddress} = {readValue}",
                    stopwatch.ElapsedMilliseconds,
                    readValue);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return new PlcConnectionTestResult(
                    true,
                    false,
                    $"连接成功，但读取 {options.HeartbeatAddress} 失败：{ex.Message}",
                    stopwatch.ElapsedMilliseconds);
            }
        }
        catch (TimeoutException)
        {
            stopwatch.Stop();
            return PlcConnectionTestResult.Fail(
                $"连接超时（{timeout.TotalMilliseconds:F0} ms）：{options.IpAddress}:{options.Port}",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "PLC 连接测试失败: {DriverType} {IpAddress}:{Port}",
                options.DriverType, options.IpAddress, options.Port);
            return PlcConnectionTestResult.Fail($"连接失败：{ex.Message}", stopwatch.ElapsedMilliseconds);
        }
        finally
        {
            if (driver != null)
            {
                try { await driver.DisconnectAsync(); } catch { /* 测试失败后的清理不覆盖原始结果 */ }
                if (driver is IDisposable disposable)
                    try { disposable.Dispose(); } catch { /* 忽略清理异常 */ }
            }
        }
    }
}
