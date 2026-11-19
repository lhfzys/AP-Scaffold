using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 插件目录清单（plugins.catalog.json）。
/// profiles 定义交付档位；plugins 定义每个插件的启用状态。
/// </summary>
public sealed class PluginCatalog
{
    private readonly Dictionary<string, List<string>> _profiles;
    private readonly Dictionary<string, PluginCatalogEntry> _plugins;

    private PluginCatalog(
        Dictionary<string, List<string>> profiles,
        Dictionary<string, PluginCatalogEntry> plugins)
    {
        _profiles = profiles;
        _plugins = plugins;
    }

    public static PluginCatalog Empty { get; } = new(
        new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Full"] = ["*"]
        },
        new Dictionary<string, PluginCatalogEntry>(StringComparer.OrdinalIgnoreCase));

    public IReadOnlyCollection<string> Profiles => _profiles.Keys;

    /// <summary>读取清单；文件不存在或损坏时返回 Empty（全部插件启用、仅 Full 档）。</summary>
    public static PluginCatalog Load(string filePath, ILogger? logger = null)
    {
        if (!File.Exists(filePath))
            return Empty;

        try
        {
            var model = JsonSerializer.Deserialize<PluginCatalogFile>(
                File.ReadAllText(filePath),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (model == null) return Empty;

            var profiles = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, ids) in model.Profiles)
                profiles[name] = ids;

            if (!profiles.ContainsKey("Full"))
                profiles["Full"] = ["*"];

            var plugins = new Dictionary<string, PluginCatalogEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var (id, entry) in model.Plugins)
                plugins[id] = entry;

            return new PluginCatalog(profiles, plugins);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "读取插件清单失败，按全部启用处理: {Path}", filePath);
            return Empty;
        }
    }

    public bool IsProfileKnown(string profile)
        => string.IsNullOrWhiteSpace(profile) || _profiles.ContainsKey(profile);

    public IReadOnlyList<string> GetProfilePlugins(string profile)
    {
        if (string.IsNullOrWhiteSpace(profile) || !_profiles.TryGetValue(profile, out var ids))
            return ["*"];
        return ids;
    }

    public bool IsIncluded(string profile, string pluginId)
    {
        var ids = GetProfilePlugins(profile);
        return ids.Any(id => id == "*" || string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));
    }

    public bool IsEnabled(string pluginId)
        => !_plugins.TryGetValue(pluginId, out var entry) || entry.Enabled;

    private sealed class PluginCatalogFile
    {
        [JsonPropertyName("profiles")]
        public Dictionary<string, List<string>> Profiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);

        [JsonPropertyName("plugins")]
        public Dictionary<string, PluginCatalogEntry> Plugins { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class PluginCatalogEntry
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = true;
}
