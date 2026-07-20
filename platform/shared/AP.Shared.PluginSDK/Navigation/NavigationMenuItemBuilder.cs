namespace AP.Shared.PluginSDK.Navigation;

/// <summary>
/// 导航菜单项构建器。
/// 负责收集各贡献者的菜单项，执行排序、去重、权限过滤和默认选中逻辑。
/// </summary>
public static class NavigationMenuItemBuilder
{
    /// <summary>
    /// 构建最终可用的导航菜单项列表。
    /// </summary>
    /// <param name="contributors">导航贡献者集合</param>
    /// <param name="hasPermission">权限检查委托；返回 true 表示有权访问</param>
    /// <param name="defaultTarget">可选的默认导航目标</param>
    /// <returns>已排序、去重、过滤后的菜单项列表</returns>
    public static IReadOnlyList<NavigationMenuItem> Build(
        IEnumerable<INavigationContributor> contributors,
        Func<string, bool> hasPermission,
        string? defaultTarget = null)
    {
        if (contributors == null) throw new ArgumentNullException(nameof(contributors));
        if (hasPermission == null) throw new ArgumentNullException(nameof(hasPermission));

        var items = contributors
            .SelectMany(c => c.GetMenuItems() ?? Enumerable.Empty<NavigationMenuItem>())
            .Where(item => !string.IsNullOrWhiteSpace(item.NavigationTarget)
                           && !string.IsNullOrWhiteSpace(item.Label))
            .GroupBy(item => item.NavigationTarget)
            .Select(g => g.OrderBy(item => item.Order).First())
            .OrderBy(item => item.Order)
            .ToList();

        var result = items
            .Select(item => new NavigationMenuItem
            {
                Label = item.Label,
                IconKind = item.IconKind,
                NavigationTarget = item.NavigationTarget,
                Order = item.Order,
                Permission = item.Permission,
                Category = item.Category,
                IsDefault = item.IsDefault
                    || (!string.IsNullOrWhiteSpace(defaultTarget)
                        && item.NavigationTarget.Equals(defaultTarget, StringComparison.OrdinalIgnoreCase))
            })
            .ToList();

        // 标记 IsDefault 后过滤权限；没有权限的项不应被默认选中
        var visibleItems = result
            .Where(item => string.IsNullOrWhiteSpace(item.Permission)
                           || hasPermission(item.Permission))
            .ToList();

        // 如果没有任何默认项，选中第一个可见项
        if (visibleItems.Count > 0 && visibleItems.All(i => !i.IsDefault))
        {
            visibleItems[0].IsDefault = true;
        }

        return result;
    }
}
