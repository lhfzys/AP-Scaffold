using System.Diagnostics;
using AP.Contracts.System.Models;
using AP.Contracts.System.Services;

namespace AP.Plugin.Layout.Services;

/// <summary>
/// 系统监控（状态栏用）：整机 CPU 占用 + 本进程内存。
/// PerformanceCounter 首次采样恒为 0（无差分基准），调用方对首个样本按无效处理。
/// 单机 WPF 场景唯一消费者在布局插件，故实现暂留在本插件（出现第二消费者再下沉 Infra）。
/// </summary>
internal sealed class SystemMonitorService : ISystemMonitorService, IDisposable
{
    private readonly PerformanceCounter _cpuCounter = new("Processor", "% Processor Time", "_Total");
    private readonly DateTime _startTime = DateTime.Now;
    private bool _primed;

    public Task<SystemMetrics> GetMetricsAsync()
    {
        // 首次调用仅建立差分基准，返回负值标记无效样本
        var cpu = _cpuCounter.NextValue();
        if (!_primed)
        {
            _primed = true;
            cpu = -1;
        }

        var metrics = new SystemMetrics
        {
            CpuUsage = Math.Round(cpu, 0),
            MemoryUsage = Math.Round(Process.GetCurrentProcess().WorkingSet64 / 1024d / 1024d, 0),
            Timestamp = DateTime.Now,
            UpTime = DateTime.Now - _startTime
        };
        return Task.FromResult(metrics);
    }

    public void Dispose() => _cpuCounter.Dispose();
}
