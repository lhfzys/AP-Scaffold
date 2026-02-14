using AP.Contracts.System.Models;

namespace AP.Contracts.System.Services;

public interface ISystemMonitorService
{
    Task<SystemMetrics> GetMetricsAsync();
}