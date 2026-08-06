namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 最新值表只读视图：订阅方读取全部 Tag 最新采集值（缓存），而不是直接打设备。
/// 实现位于 Infra（LatestTagValueStore）；UI/业务只允许经本契约访问（分层防线，见 docs/conventions/LAYERING.md）。
/// </summary>
public interface ILatestTagValueStore
{
    /// <summary>全部最新值快照（未采集过的点不在其中）。</summary>
    IReadOnlyDictionary<string, TagValue> Snapshot();
}
