namespace AP.Shared.PluginSDK.Navigation;

/// <summary>
/// 导航贡献者接口。
/// 由需要暴露侧边栏导航项的插件实现，供布局插件统一收集和渲染。
/// </summary>
public interface INavigationContributor
{
    /// <summary>
    /// 返回本插件提供的导航菜单项集合。
    /// </summary>
    IEnumerable<NavigationMenuItem> GetMenuItems();
}
