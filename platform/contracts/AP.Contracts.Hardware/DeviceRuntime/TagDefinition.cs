namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// Tag 定义（**纯配置形状**：这个点是什么、在哪里）。
/// 分层纪律：
/// - Address 是配置表示（字符串），运行时由各驱动的 Address Object 解析并缓存于 Infra 层，契约层不持有协议类型；
/// - 采集策略（周期/开关）不属于本类——那是采集配置（T4.4）的职责；
/// - 质量语义只属于 TagValue/Tag 服务层。
/// </summary>
public sealed class TagDefinition
{
    /// <summary>逻辑点名（全系统唯一），如 "Line1.Oven.Temperature"。</summary>
    public required string Name { get; init; }

    /// <summary>引用设备注册表的 DeviceId（如 "plc.main"）。</summary>
    public required string DeviceId { get; init; }

    /// <summary>协议地址（配置表示；合法性由驱动的 Address Object 在点表加载时校验）。</summary>
    public required string Address { get; init; }

    /// <summary>数据类型。</summary>
    public TagDataType DataType { get; init; } = TagDataType.Int16;

    /// <summary>读写方向。</summary>
    public TagAccess Access { get; init; } = TagAccess.ReadWrite;

    /// <summary>描述（预留：点表/设备管理界面展示，当前无消费者）。</summary>
    public string? Description { get; init; }

    /// <summary>分组（预留：采集配置与界面按组管理，当前无消费者）。</summary>
    public string? Group { get; init; }

    /// <summary>工程单位（预留：如 "℃"、"MPa"，当前无消费者）。</summary>
    public string? Unit { get; init; }
}
