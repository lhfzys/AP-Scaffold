#region

using System.Text.Json;
using System.Text.Json.Nodes;
using AP.Shared.Utilities.Paths;

#endregion

namespace AP.Shared.Utilities.Helpers;

public static class ConfigurationHelper
{
    /// <summary>
    ///     解析配置节的写回目标文件：活动角色文件（数据根覆盖层或安装目录默认角色文件）中存在该节 → 角色文件；否则 appsettings.json。
    /// </summary>
    /// <remarks>
    ///     配置按“安装目录默认值 + 数据根覆盖层”分层加载（覆盖层优先）。
    ///     只存在于角色文件中的节（如 Plugins:Configuration:AP.Plugin.Scanner）若写回基文件，
    ///     会被角色文件遮蔽、永不生效。
    /// </remarks>
    /// <param name="sectionName">节点路径，例如 "Plugins:Configuration:AP.Plugin.Scanner"</param>
    /// <param name="roleFileName">活动角色配置文件名（宿主启动时写入 AppRuntime:RoleConfigFile）；空或文件不存在时回退基文件</param>
    public static string ResolveTargetFileName(string sectionName, string? roleFileName)
    {
        if (!string.IsNullOrWhiteSpace(roleFileName))
        {
            var overlayRolePath = Path.Combine(AppPaths.ConfigDirectory, roleFileName);
            var defaultRolePath = Path.Combine(AppPaths.DefaultConfigDirectory, roleFileName);
            if ((File.Exists(overlayRolePath) && SectionExists(overlayRolePath, sectionName))
                || (File.Exists(defaultRolePath) && SectionExists(defaultRolePath, sectionName)))
                return roleFileName;
        }

        return "appsettings.json";
    }

    /// <summary>判断 JSON 配置文件中是否存在指定节点路径（文件损坏时按不存在处理）。</summary>
    private static bool SectionExists(string filePath, string sectionName)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(filePath));
        }
        catch (JsonException)
        {
            return false;
        }

        foreach (var part in sectionName.Split(':'))
        {
            if (node is not JsonObject obj) return false;
            node = obj[part];
            if (node is null) return false;
        }

        return true;
    }

    /// <summary>
    ///     将配置节点安全地更新到数据根的可写覆盖层（原子写入：先写临时文件再替换，避免中途崩溃损坏配置）。
    /// </summary>
    /// <param name="sectionName">节点路径，例如 "Plugins:Scanner:SerialPort"</param>
    /// <param name="newValue">新的配置对象</param>
    /// <param name="fileName">配置文件名（默认 appsettings.json）</param>
    /// <exception cref="ArgumentException">节点路径为空</exception>
    /// <exception cref="JsonException">配置文件内容不是合法 JSON</exception>
    /// <exception cref="IOException">读写配置文件失败</exception>
    /// <exception cref="UnauthorizedAccessException">无配置文件读写权限</exception>
    /// <remarks>
    ///     覆盖层文件不存在时会创建；只写指定节点，不复制安装目录的默认值，
    ///     从而让“默认值 + 覆盖层”的配置合并继续生效。
    /// </remarks>
    public static void UpdateAppSetting<T>(string sectionName, T newValue, string fileName = "appsettings.json")
    {
        if (string.IsNullOrWhiteSpace(sectionName))
            throw new ArgumentException("配置节点路径不能为空", nameof(sectionName));

        var configDirectory = AppPaths.ConfigDirectory;
        Directory.CreateDirectory(configDirectory);

        var filePath = Path.Combine(configDirectory, fileName);
        var jsonNode = File.Exists(filePath)
            ? JsonNode.Parse(File.ReadAllText(filePath))
            : new JsonObject();

        if (jsonNode is not JsonObject root)
            throw new JsonException($"配置文件根节点不是 JSON 对象: {filePath}");

        // 支持形如 "Plugins:Scanner:SerialPort" 的多级节点解析
        var sections = sectionName.Split(':');
        JsonNode currentNode = root;

        for (var i = 0; i < sections.Length - 1; i++)
        {
            if (currentNode[sections[i]] is not JsonObject)
                currentNode[sections[i]] = new JsonObject();

            currentNode = currentNode[sections[i]]!;
        }

        var finalSection = sections.Last();
        var newNode = JsonSerializer.SerializeToNode(newValue, new JsonSerializerOptions { WriteIndented = true });

        // 合并而不是整节替换：保存“应用基础信息”时不能把同节的
        // LayoutMode / DefaultNavigationTarget / NavigationWhenSecurityDisabled 或插件的未知键抹掉。
        if (newNode is JsonObject newObject && currentNode[finalSection] is JsonObject existingObject)
            MergeJsonObject(existingObject, newObject);
        else
            currentNode[finalSection] = newNode;

        var options = new JsonSerializerOptions { WriteIndented = true };
        var content = root.ToJsonString(options);

        // 原子替换：同目录临时文件 + Move(overwrite)，避免写入中途失败留下半截文件
        var tempPath = filePath + ".tmp";
        File.WriteAllText(tempPath, content);
        File.Move(tempPath, filePath, true);
    }

    /// <summary>
    /// 递归合并 JSON 对象：已有键保留，新对象中出现的键覆盖/新增；数组整体替换。
    /// </summary>
    private static void MergeJsonObject(JsonObject target, JsonObject source)
    {
        foreach (var property in source)
        {
            if (target[property.Key] is JsonObject targetChild && property.Value is JsonObject sourceChild)
                MergeJsonObject(targetChild, sourceChild);
            else
                target[property.Key] = property.Value?.DeepClone();
        }
    }
}
