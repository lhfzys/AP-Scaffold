namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 采集引擎运行状态只读视图（是否在采集 + 读次统计）。
/// 实现位于 Infra（TagAcquisitionEngine）；UI/业务只允许经本契约访问（分层防线，见 docs/conventions/LAYERING.md）。
/// </summary>
public interface ITagAcquisitionStatus
{
    /// <summary>采集引擎是否正在运行。</summary>
    bool IsRunning { get; }

    /// <summary>自启动以来累计读取点数（含失败）。</summary>
    long TotalReads { get; }

    /// <summary>自启动以来累计失败点数（Bad 质量/读取异常）。</summary>
    long FailedReads { get; }
}
