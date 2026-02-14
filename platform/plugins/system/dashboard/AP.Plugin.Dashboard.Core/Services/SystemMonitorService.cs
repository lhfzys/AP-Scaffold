using Microsoft.Extensions.Logging;
using System.Diagnostics;
using AP.Contracts.System.Models;
using AP.Contracts.System.Services;

namespace AP.Plugin.Dashboard.Core.Services;

public class SystemMonitorService : ISystemMonitorService, IDisposable
{
    private readonly ILogger _logger;
    private readonly PerformanceCounter? _cpuCounter;
    private readonly Process _currentProcess;

    public SystemMonitorService(ILogger<SystemMonitorService> logger)
    {
        _logger = logger;
        _currentProcess = Process.GetCurrentProcess();

        try
        {
            // Windows 下获取 CPU 使用率
            if (OperatingSystem.IsWindows())
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _cpuCounter.NextValue(); // 第一次调用通常为0，先预热
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("无法初始化性能计数器 (非 Windows 环境或权限不足): {Msg}", ex.Message);
        }
    }

    public Task<SystemMetrics> GetMetricsAsync()
    {
        var metrics = new SystemMetrics
        {
            Timestamp = DateTime.Now,
            // 获取当前进程的内存占用 (MB)
            MemoryUsage = _currentProcess.WorkingSet64 / 1024.0 / 1024.0,
            // 获取系统运行时间
            UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64)
        };

        if (_cpuCounter != null) metrics.CpuUsage = _cpuCounter.NextValue();

        return Task.FromResult(metrics);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _currentProcess.Dispose();
    }
}