using System.Text.Json;
using System.Text.Json.Serialization;
using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// 点表加载器（启动快速失败 + 运行期热重载）。
/// 启动时从 JSON 文件加载点表并全量校验（点名唯一 / 设备存在 / 地址经驱动验证器解析），
/// 全部错误聚合后以 <see cref="DeviceConfigurationException"/> 一次性抛出。
/// 校验通过的 Tag 以 <see cref="ResolvedTag"/>（含缓存的 Address Object）形式提供。
/// 热重载（<see cref="Reload"/>）复用同一校验：失败保留旧表继续运行，成功则原子替换快照。
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

    private readonly object _gate = new();
    private readonly IDeviceRegistry _deviceRegistry;
    private readonly IReadOnlyDictionary<string, IAddressValidator> _validators;
    private readonly string _filePath;

    private IReadOnlyDictionary<string, ResolvedTag> _tags;

    public TagTable(
        IDeviceRegistry deviceRegistry,
        IEnumerable<IAddressValidator> addressValidators,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(deviceRegistry);
        ArgumentNullException.ThrowIfNull(addressValidators);

        _deviceRegistry = deviceRegistry;
        _validators = addressValidators.ToDictionary(v => v.DriverType, StringComparer.OrdinalIgnoreCase);
        _filePath = filePath;

        var file = LoadFile(filePath);
        var errors = new List<string>();
        _tags = TagTableValidation.ValidateAndResolve(file.Tags, deviceRegistry, _validators, errors);
        if (errors.Count > 0)
            throw new DeviceConfigurationException(
                $"点表校验失败（{errors.Count} 处）:{Environment.NewLine} - {string.Join($"{Environment.NewLine} - ", errors)}");

        Acquisition = file.Acquisition ?? new TagAcquisitionConfig();
    }

    /// <inheritdoc />
    public TagAcquisitionConfig Acquisition { get; private set; }

    /// <inheritdoc />
    public IReadOnlyCollection<ResolvedTag> Tags
    {
        get { lock (_gate) return _tags.Values.ToList(); }
    }

    /// <inheritdoc />
    public ResolvedTag? Find(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        lock (_gate) return _tags.TryGetValue(name, out var tag) ? tag : null;
    }

    /// <summary>
    /// 热重载：重读文件并全量校验，成功则原子替换点表与采集配置快照；
    /// 失败（文件读取/校验错误）保留旧表，返回错误明细。
    /// 采集分组重建与最新值表清理由 <see cref="TagTableReloader"/> 编排。
    /// </summary>
    public IReadOnlyList<string> Reload()
    {
        TagTableFile file;
        try
        {
            file = LoadFile(_filePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException
            or DeviceConfigurationException)
        {
            return [$"点表文件读取失败: {ex.Message}"];
        }

        var errors = new List<string>();
        var tags = TagTableValidation.ValidateAndResolve(file.Tags, _deviceRegistry, _validators, errors);
        if (errors.Count > 0)
            return errors;

        lock (_gate)
        {
            _tags = tags;
            Acquisition = file.Acquisition ?? new TagAcquisitionConfig();
        }
        return errors;
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
