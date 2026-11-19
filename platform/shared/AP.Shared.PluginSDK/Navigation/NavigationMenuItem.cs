namespace AP.Shared.PluginSDK.Navigation;

/// <summary>
/// 导航菜单项定义，由插件通过 <see cref="INavigationContributor"/> 声明。
/// </summary>
public sealed class NavigationMenuItem
{
    /// <summary>
    /// 显示文本
    /// </summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Material Design 图标名称（例如 "ViewDashboard"、"Cog"）
    /// </summary>
    public string IconKind { get; set; } = string.Empty;

    /// <summary>
    /// 导航目标视图名称（需有对应视图注册到 ContentRegion）
    /// </summary>
    public string NavigationTarget { get; set; } = string.Empty;

    /// <summary>
    /// 排序权重，越小越靠前
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// 可选权限码；为空表示无需权限
    /// </summary>
    public string? Permission { get; set; }

    /// <summary>
    /// 分组名称，用于未来二级菜单或抽屉分组；当前可留空
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// 是否作为启动默认页
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 免登录（Security 禁用）时是否始终显示。
    /// 用于系统诊断/插件状态等运维页面，避免旧版配置文件的白名单没有新页面而不可见。
    /// </summary>
    public bool ShowWhenSecurityDisabled { get; set; }
}
