using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 点表全量校验与解析（<see cref="TagTable"/> 启动加载与 <see cref="TagTableValidator"/> 共用）。
/// 规则：点名非空且唯一（大小写不敏感）/ 引用设备已注册 / 地址经对应驱动验证器解析。
/// </summary>
internal static class TagTableValidation
{
    /// <summary>
    /// 校验并解析点定义。校验通过的条目以 <see cref="ResolvedTag"/> 形式返回；
    /// 全部错误聚合到 <paramref name="errors"/>（不中断、不抛出）。
    /// </summary>
    public static IReadOnlyDictionary<string, ResolvedTag> ValidateAndResolve(
        IEnumerable<TagDefinition> definitions,
        IDeviceRegistry deviceRegistry,
        IReadOnlyDictionary<string, IAddressValidator> validators,
        ICollection<string> errors)
    {
        var resolved = new Dictionary<string, ResolvedTag>(StringComparer.OrdinalIgnoreCase);

        foreach (var tag in definitions)
        {
            if (string.IsNullOrWhiteSpace(tag.Name))
            {
                errors.Add("存在点名为空的条目");
                continue;
            }

            if (resolved.ContainsKey(tag.Name))
            {
                errors.Add($"点名重复: '{tag.Name}'");
                continue;
            }

            var device = deviceRegistry.Find(tag.DeviceId);
            if (device == null)
            {
                errors.Add($"点 '{tag.Name}' 引用的设备未注册: '{tag.DeviceId}'");
                continue;
            }

            if (!validators.TryGetValue(device.Info.DriverType, out var validator))
            {
                errors.Add($"点 '{tag.Name}' 的驱动 '{device.Info.DriverType}' 无地址验证器");
                continue;
            }

            if (!validator.TryParse(tag.Address, out var parsed, out var error))
            {
                errors.Add($"点 '{tag.Name}' 地址非法: {error}");
                continue;
            }

            resolved[tag.Name] = new ResolvedTag(tag, parsed!);
        }

        return resolved;
    }
}
