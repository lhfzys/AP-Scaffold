#region

using System.Text.Json;
using System.Text.Json.Nodes;

#endregion

namespace AP.Shared.Utilities.Helpers;

public static class ConfigurationHelper
{
    /// <summary>
    ///     将配置节点安全地更新到 appsettings.json
    /// </summary>
    /// <param name="sectionName">节点路径，例如 "Plugins:Scanner:SerialPort"</param>
    /// <param name="newValue">新的配置对象</param>
    /// <param name="fileName">配置文件名（默认 appsettings.json）</param>
    public static void UpdateAppSetting<T>(string sectionName, T newValue, string fileName = "appsettings.json")
    {
        try
        {
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration", fileName);
            if (!File.Exists(filePath)) return;

            var json = File.ReadAllText(filePath);
            var jsonNode = JsonNode.Parse(json);

            if (jsonNode == null) return;

            // 支持形如 "Plugins:Scanner:SerialPort" 的多级节点解析
            var sections = sectionName.Split(':');
            var currentNode = jsonNode;

            for (var i = 0; i < sections.Length - 1; i++)
            {
                if (currentNode?[sections[i]] == null) currentNode[sections[i]] = new JsonObject();
                currentNode = currentNode[sections[i]];
            }

            var finalSection = sections.Last();
            currentNode[finalSection] =
                JsonSerializer.SerializeToNode(newValue, new JsonSerializerOptions { WriteIndented = true });

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(filePath, jsonNode.ToJsonString(options));
        }
        catch (Exception ex)
        {
            // 在实际项目中可接入 Logger，这里为了简单直接向上抛出或静默处理
            Console.WriteLine($"更新配置文件失败: {ex.Message}");
        }
    }
}