namespace AP.Contracts.Hardware.DeviceRuntime;

/// <summary>
/// 已解析 Tag（运行时模型）：定义 + 缓存的 Address Object。
/// ParsedAddress 对 Tag 层不透明（驱动内部类型），其 ToString() 即规范化地址；
/// 仅所属驱动可在批量合并等场景还原使用。
/// </summary>
public sealed record ResolvedTag(TagDefinition Definition, object ParsedAddress)
{
    /// <summary>规范化地址（等价于 ParsedAddress.ToString()）。</summary>
    public string NormalizedAddress => ParsedAddress.ToString() ?? Definition.Address;
}
