namespace AP.Core.PluginFramework.Loading;

/// <summary>
/// 插件图校验中发现的问题
/// </summary>
/// <param name="PluginId">相关插件 ID</param>
/// <param name="Message">问题描述</param>
/// <param name="IsFatal">是否致命（重复 ID 或 Required 插件依赖缺失）</param>
public record PluginGraphIssue(string PluginId, string Message, bool IsFatal);

/// <summary>
/// 插件图校验器（重复 ID 检测 + 依赖完整性过滤）
/// </summary>
public static class PluginGraphValidator
{
    /// <summary>
    /// 校验插件集合并返回允许加载的插件：
    /// - 重复 ID：属于部署错误，所有副本全部剔除（致命问题）；
    /// - 依赖缺失：剔除该插件（Required 插件为致命问题），级联直到收敛。
    /// </summary>
    /// <param name="descriptors">发现的插件描述符</param>
    /// <param name="issues">校验发现的问题列表</param>
    /// <returns>通过校验、允许加载的插件列表</returns>
    public static List<PluginDescriptor> Validate(IReadOnlyList<PluginDescriptor> descriptors, out List<PluginGraphIssue> issues)
    {
        issues = new List<PluginGraphIssue>();
        var result = new List<PluginDescriptor>(descriptors);

        // 1. 重复 ID 检测：同一 ID 出现多份属于部署错误，全部拒绝加载
        foreach (var group in result.GroupBy(d => d.Metadata.Id).Where(g => g.Count() > 1))
        {
            issues.Add(new PluginGraphIssue(
                group.Key,
                $"插件 ID 重复: {group.Key}（共 {group.Count()} 份），已全部拒绝加载",
                true));
            result.RemoveAll(d => d.Metadata.Id == group.Key);
        }

        // 2. 依赖完整性检查（级联：被剔除插件的依赖方也会被剔除，直到收敛）
        bool removed;
        do
        {
            removed = false;
            var availableIds = result.Select(d => d.Metadata.Id).ToHashSet();

            foreach (var descriptor in result.ToList())
            {
                var missing = descriptor.Metadata.Dependencies.Where(dep => !availableIds.Contains(dep)).ToList();
                if (missing.Count == 0) continue;

                issues.Add(new PluginGraphIssue(
                    descriptor.Metadata.Id,
                    $"插件 {descriptor.Metadata.Id} 依赖缺失: {string.Join(", ", missing)}，已拒绝加载",
                    descriptor.Metadata.Required));
                result.Remove(descriptor);
                removed = true;
            }
        } while (removed);

        return result;
    }
}
