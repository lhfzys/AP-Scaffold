using System.Text.Json;
using System.Text.Json.Serialization;

namespace AP.Shared.Utilities.Helpers;

/// <summary>
/// 统一序列化助手 (封装 System.Text.Json)
/// </summary>
public static class SerializationHelper
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true, // 忽略大小写
        WriteIndented = false, // 生产环境不缩进，节省流量
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 忽略 null 值
        Converters = { new JsonStringEnumConverter() } // 枚举转字符串
    };

    /// <summary>
    /// 对象转 JSON 字符串
    /// </summary>
    public static string ToJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, _options);
    }

    /// <summary>
    /// JSON 字符串转对象
    /// </summary>
    public static T? FromJson<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, _options);
    }

    /// <summary>
    /// JSON 字符串转 Object (尝试还原基础类型)
    /// </summary>
    public static object? FromJson(string json, Type targetType)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize(json, targetType, _options);
    }
}