using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Plugin.TagConfiguration.Services;

/// <summary>
/// tags.json 文件存取（点表配置界面专用）。
/// 读取选项与 Infra 层 TagTable 加载器一致（忽略大小写 / 跳过注释 / 允许尾逗号 / 枚举字符串）；
/// 写入为"临时文件 + 替换"原子写入，并在文件头补写标准注释（JSON 序列化不保留注释）。
/// </summary>
internal static class TagTableFileStore
{
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private const string HeaderComment = """
        // 点表（Tag Table）：业务按逻辑点名读写设备数据，点名 → 设备 + 协议地址的映射在此维护。
        // 启动时全量校验（点名唯一 / 设备已注册 / 地址合法），任一非法即中止启动（快速失败）。
        // 本文件由"点表配置"界面维护，也可手工编辑；修改后需重启应用生效。

        """;

    public static string FilePath => Path.Combine(AppContext.BaseDirectory, "Configuration", "tags.json");

    /// <summary>读取点表文件；文件缺失按空表处理（与 TagTable 行为一致）。</summary>
    public static TagTableFileData Load()
    {
        if (!File.Exists(FilePath)) return new TagTableFileData();

        var json = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<TagTableFileData>(json, ReadOptions) ?? new TagTableFileData();
    }

    /// <summary>原子写入点表文件（覆盖式，含头部标准注释）。</summary>
    public static void Save(TagTableFileData data)
    {
        var content = HeaderComment + JsonSerializer.Serialize(data, WriteOptions);
        var tempPath = FilePath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, FilePath, true);
    }
}

/// <summary>点表文件形状：{ "Acquisition": {...}, "Tags": [ TagDefinition... ] }。</summary>
internal sealed class TagTableFileData
{
    public TagAcquisitionConfig Acquisition { get; set; } = new();

    public List<TagDefinition> Tags { get; set; } = [];
}
