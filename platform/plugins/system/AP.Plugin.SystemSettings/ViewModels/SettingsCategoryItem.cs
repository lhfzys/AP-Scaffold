using AP.Shared.PluginSDK.Configuration;

namespace AP.Plugin.SystemSettings.ViewModels;

/// <summary>
/// 左侧导航条目公共接口
/// </summary>
public interface INavigationItem
{
    /// <summary>
    /// 是否可作为选中项
    /// </summary>
    bool IsSelectable { get; }
}

/// <summary>
/// 设置分类项
/// 用于左侧导航分组展示。
/// </summary>
public class SettingsCategoryItem
{
    public string Category { get; }

    public SettingsCategoryItem(string category)
    {
        Category = category;
    }

    /// <summary>
    /// 该分类下的配置贡献者
    /// </summary>
    public List<SettingsContributorItem> Contributors { get; } = new();
}

/// <summary>
/// 左侧导航分类标题项（不可选中）
/// </summary>
public class SettingsCategoryHeaderItem : INavigationItem
{
    public string Category { get; }

    public bool IsSelectable => false;

    public SettingsCategoryHeaderItem(string category)
    {
        Category = category;
    }
}

/// <summary>
/// 配置贡献者展示项
/// </summary>
public class SettingsContributorItem : INavigationItem
{
    public ISettingsContributor Contributor { get; }
    public ISettingsEditorViewModel Editor { get; }

    public string Title => Contributor.Title;
    public string? IconKind => Contributor.IconKind;
    public string Category => Contributor.Category;

    public bool IsSelectable => true;

    public SettingsContributorItem(ISettingsContributor contributor, ISettingsEditorViewModel editor)
    {
        Contributor = contributor;
        Editor = editor;
    }
}
