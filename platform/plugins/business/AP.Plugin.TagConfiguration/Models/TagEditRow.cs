using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Plugin.TagConfiguration.Models;

/// <summary>
/// 点表编辑行（列表与编辑窗共用的可编辑模型）。
/// 采集周期覆盖为空表示跟随默认周期（对应 tags.json Acquisition.Overrides 无此项）。
/// </summary>
public sealed class TagEditRow
{
    public string Name { get; set; } = string.Empty;

    public string DeviceId { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public TagDataType DataType { get; set; } = TagDataType.Int16;

    public TagAccess Access { get; set; } = TagAccess.ReadWrite;

    public string? Description { get; set; }

    public string? Group { get; set; }

    public string? Unit { get; set; }

    /// <summary>采集周期覆盖（毫秒）；null = 跟随默认。</summary>
    public int? IntervalOverrideMs { get; set; }

    /// <summary>采集周期列显示：覆盖值或"默认"。</summary>
    public string IntervalDisplay => IntervalOverrideMs?.ToString() ?? "默认";

    /// <summary>转为点表定义（保存用）。</summary>
    public TagDefinition ToDefinition() => new()
    {
        Name = Name.Trim(),
        DeviceId = DeviceId,
        Address = Address.Trim(),
        DataType = DataType,
        Access = Access,
        Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        Group = string.IsNullOrWhiteSpace(Group) ? null : Group.Trim(),
        Unit = string.IsNullOrWhiteSpace(Unit) ? null : Unit.Trim()
    };

    public TagEditRow Clone() => (TagEditRow)MemberwiseClone();
}
