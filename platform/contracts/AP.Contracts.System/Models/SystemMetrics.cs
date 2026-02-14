namespace AP.Contracts.System.Models;

public class SystemMetrics
{
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; } // MB
    public DateTime Timestamp { get; set; }
    public TimeSpan UpTime { get; set; }
}