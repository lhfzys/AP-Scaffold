using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 点表校验器（<see cref="ITagTableValidator"/> 实现）：
/// 与 <see cref="TagTable"/> 启动加载共用 <see cref="TagTableValidation"/>，规则与错误文案完全一致。
/// 供点表编辑界面保存前预检，校验失败不抛异常、返回错误列表。
/// </summary>
public sealed class TagTableValidator : ITagTableValidator
{
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IReadOnlyDictionary<string, IAddressValidator> _validators;

    public TagTableValidator(IDeviceRegistry deviceRegistry, IEnumerable<IAddressValidator> addressValidators)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistry);
        ArgumentNullException.ThrowIfNull(addressValidators);

        _deviceRegistry = deviceRegistry;
        _validators = addressValidators.ToDictionary(v => v.DriverType, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Validate(IReadOnlyList<TagDefinition> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var errors = new List<string>();
        TagTableValidation.ValidateAndResolve(tags, _deviceRegistry, _validators, errors);
        return errors;
    }
}
