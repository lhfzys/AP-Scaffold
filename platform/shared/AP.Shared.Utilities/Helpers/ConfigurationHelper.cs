#region

using System.Text.Json;
using System.Text.Json.Nodes;

#endregion

namespace AP.Shared.Utilities.Helpers;

public static class ConfigurationHelper
{
    /// <summary>
    ///     将配置节点安全地更新到 appsettings.json（原子写入：先写临时文件再替换，避免中途崩溃损坏配置）
    /// </summary>
    /// <param name="sectionName">节点路径，例如 "Plugins:Scanner:SerialPort"</param>
    /// <param name="newValue">新的配置对象</param>
    /// <param name="fileName">配置文件名（默认 appsettings.json）</param>
    /// <exception cref="ArgumentException">节点路径为空</exception>
    /// <exception cref="JsonException">配置文件内容不是合法 JSON</exception>
    /// <exception cref="IOException">读写配置文件失败</exception>
    /// <exception cref="UnauthorizedAccessException">无配置文件读写权限</exception>
    /// <remarks>配置文件不存在时不做任何操作（静默返回）；其他错误一律抛出，由调用方处理。</remarks>
    public static void UpdateAppSetting<T>(string sectionName, T newValue, string fileName = "appsettings.json")
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            throw new ArgumentException("配置节点路径不能为空", nameof(sectionName));

        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", fileName);
        if (!File.Exists(filePath)) return;

        var json = File.ReadAllText(filePath);
        var jsonNode = JsonNode.Parse(json);

        if (jsonNode == null) return;

        // 支持形如 "Plugins:Scanner:SerialPort" 的多级节点解析
        var sections = sectionName.Split(':');
        JsonNode currentNode = jsonNode;

        for (var i = 0; i < sections.Length - 1; i++)
        {
            if (currentNode[sections[i]] == null) currentNode[sections[i]] = new JsonObject();
            currentNode = currentNode[sections[i]]!;
        }

        var finalSection = sections.Last();
        currentNode[finalSection] =
            JsonSerializer.SerializeToNode(newValue, new JsonSerializerOptions { WriteIndented = true });

        var options = new JsonSerializerOptions { WriteIndented = true };
        var content = jsonNode.ToJsonString(options);

        // 原子替换：同目录临时文件 + Move(overwrite)，避免写入中途失败留下半截文件
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, filePath, true);
    }
}