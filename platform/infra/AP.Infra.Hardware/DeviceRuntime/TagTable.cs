using System.Text.Json;
using System.Text.Json.Serialization;
using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 点表加载器（快速失败策略）。
/// 启动时从 JSON 文件加载点表并全量校验（点名唯一 / 设备存在 / 地址经驱动验证器解析），
/// 全部错误聚合后以 <see cref="DeviceConfigurationException"/> 一次性抛出。
/// 校验通过的 Tag 以 <see cref="ResolvedTag"/>（含缓存的 Address Object）形式提供，运行期只读。
/// </summary>
public sealed class TagTable : ITagTable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyDictionary<string, ResolvedTag> _tags;

    public TagTable(
        IDeviceRegistry deviceRegistry,
        IEnumerable<IAddressValidator> addressValidators,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistry);
        ArgumentNullException.ThrowIfNull(addressValidators);

        var validators = addressValidators.ToDictionary(v => v.DriverType, StringComparer.OrdinalIgnoreCase);
        var definitions = LoadDefinitions(filePath);
        _tags = ValidateAndResolve(definitions, deviceRegistry, validators);
    }

    /// <inheritdoc />
    public IReadOnlyCollection<ResolvedTag> Tags => _tags.Values.ToList();

    /// <inheritdoc />
    public ResolvedTag? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tags.TryGetValue(name, out var tag) ? tag : null;
    }

    /// <summary>文件缺失视为空点表（合法：设备可纯手动访问）。</summary>
    private static List<TagDefinition> LoadDefinitions(string filePath)
    {
        if (!File.Exists(filePath)) return [];

        var json = File.ReadAllText(filePath);
        var file = JsonSerializer.Deserialize<TagTableFile>(json, JsonOptions)
            ?? throw new DeviceConfigurationException($"点表文件为空或格式错误: {filePath}");
        return file.Tags;
    }

    private static IReadOnlyDictionary<string, ResolvedTag> ValidateAndResolve(
        List<TagDefinition> definitions,
        IDeviceRegistry deviceRegistry,
        IReadOnlyDictionary<string, IAddressValidator> validators)
    {
        var errors = new List<string>();
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

        if (errors.Count > 0)
            throw new DeviceConfigurationException(
                $"点表校验失败（{errors.Count} 处）:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}");

        return resolved;
    }

    /// <summary>点表文件形状：{ "Tags": [ TagDefinition... ] }。</summary>
    private sealed class TagTableFile
    {
        public List<TagDefinition> Tags { get; set; } = [];
    }
}
