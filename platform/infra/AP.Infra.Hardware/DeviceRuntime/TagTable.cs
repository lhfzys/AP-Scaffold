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
        var file = LoadFile(filePath);
        Acquisition = file.Acquisition ?? new TagAcquisitionConfig();

        var errors = new List<string>();
        _tags = TagTableValidation.ValidateAndResolve(file.Tags, deviceRegistry, validators, errors);
        if (errors.Count > 0)
            throw new DeviceConfigurationException(
                $"点表校验失败（{errors.Count} 处）:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}");
    }

    /// <summary>采集配置（tags.json "Acquisition" 节；缺失=默认值）。</summary>
    public TagAcquisitionConfig Acquisition { get; }

    /// <inheritdoc />
    public IReadOnlyCollection<ResolvedTag> Tags => _tags.Values.ToList();

    /// <inheritdoc />
    public ResolvedTag? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _tags.TryGetValue(name, out var tag) ? tag : null;
    }

    /// <summary>文件缺失视为空点表（合法：设备可纯手动访问）。</summary>
    private static TagTableFile LoadFile(string filePath)
    {
        if (!File.Exists(filePath)) return new TagTableFile();

        var json = File.ReadAllText(filePath);
        return JsonSerializer.Deserialize<TagTableFile>(json, JsonOptions)
            ?? throw new DeviceConfigurationException($"点表文件为空或格式错误: {filePath}");
    }

    /// <summary>点表文件形状：{ "Acquisition": {...}, "Tags": [ TagDefinition... ] }。</summary>
    private sealed class TagTableFile
    {
        public TagAcquisitionConfig? Acquisition { get; set; }
        public List<TagDefinition> Tags { get; set; } = [];
    }
}
