namespace AP.Core.Enums;

/// <summary>
/// 应用运行角色 (支持位运算组合)
/// </summary>
[Flags]
public enum AppRole
{
    None = 0,

    /// <summary>
    /// 客户端模式 (UI, 远程连接)
    /// </summary>
    Client = 1 << 0,

    /// <summary>
    /// 服务端模式 (无头模式, 数据处理, 硬件连接)
    /// </summary>
    Server = 1 << 1,

    /// <summary>
    /// 单机模式 (全功能本地运行)
    /// </summary>
    Standalone = 1 << 2,

    /// <summary>
    /// 所有角色
    /// </summary>
    All = Client | Server | Standalone
}