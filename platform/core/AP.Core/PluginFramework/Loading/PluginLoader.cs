using AP.Core.Enums;
using AP.Shared.Utilities.Constants;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 插件加载器 (负责发现和加载插件程序集)
/// 负责遍历目录 -> 创建隔离上下文 -> 加载 DLL -> 扫描类型 -> 校验角色 -> 返回描述符。
/// </summary>
public class PluginLoader
{
    private readonly ILogger _logger;

    public PluginLoader(ILogger<PluginLoader> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// 发现并加载指定目录下的所有插件
    /// </summary>
    /// <param name="currentRole">当前应用运行的角色 (Server/Client)，用于过滤不适用的插件</param>
    /// <returns>已加载的插件描述符列表</returns>
    public List<PluginDescriptor> DiscoverPlugins(AppRole currentRole)
    {
        var descriptors = new List<PluginDescriptor>();

        // 获取插件根目录: AppContext.BaseDirectory + "plugins"
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, GlobalConstants.Paths.Plugins);

        if (!Directory.Exists(pluginsDir))
        {
            _logger.LogWarning("插件目录不存在: {Path}", pluginsDir);
            return descriptors;
        }

        // 遍历 plugins 目录下的每个子目录 (每个插件一个文件夹)
        foreach (var dir in Directory.GetDirectories(pluginsDir))
            try
            {
                var descriptor = LoadPluginFromDirectory(dir, currentRole);
                if (descriptor != null) descriptors.Add(descriptor);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载插件目录失败: {Dir}", dir);
            }

        // 按优先级排序 (数值越小越优先)
        return descriptors.OrderBy(d => d.Metadata.Priority).ToList();
    }

    private PluginDescriptor? LoadPluginFromDirectory(string pluginDir, AppRole currentRole)
    {
        var dirName = Path.GetFileName(pluginDir);
        // 约定：主程序集名称必须与目录名一致 (例如 plugins/AP.Plugin.Demo/AP.Plugin.Demo.dll)
        var dllPath = Path.Combine(pluginDir, $"{dirName}.dll");

        if (!File.Exists(dllPath))
        {
            _logger.LogWarning("在插件目录中未找到主程序集: {Path}", dllPath);
            return null;
        }

        // 1. 创建隔离上下文
        var context = new PluginLoadContext(dllPath);

        try
        {
            // 2. 加载程序集
            var assembly = context.LoadFromAssemblyPath(dllPath);

            // 3. 扫描 IPlugin 实现
            var pluginType = AssemblyScanner.ScanForPluginType(assembly);
            if (pluginType == null)
            {
                _logger.LogWarning("程序集 {Dll} 中未找到 IPlugin 实现", dirName);
                context.Unload();
                return null;
            }

            // 4. 获取元数据
            var metadata = AssemblyScanner.GetMetadata(pluginType);
            if (metadata == null)
            {
                _logger.LogError("插件类 {Type} 缺少 [PluginMetadata] 特性", pluginType.Name);
                context.Unload();
                return null;
            }

            // 5. 检查角色匹配 (核心逻辑: 如果是 Server 模式，就不加载纯 Client 插件)
            if ((metadata.SupportedRoles & currentRole) == 0)
            {
                context.Unload();
                return null;
            }

            _logger.LogInformation("发现插件: {Name} (v{Version})", metadata.Name, metadata.Version);
            return new PluginDescriptor(metadata, pluginType, context, assembly);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载插件程序集失败: {DllPath}", dllPath);
            context.Unload(); // 发生异常时卸载上下文
            return null;
        }
    }
}