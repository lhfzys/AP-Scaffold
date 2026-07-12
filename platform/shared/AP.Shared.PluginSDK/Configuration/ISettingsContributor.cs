namespace AP.Shared.PluginSDK.Configuration;

/// <summary>
/// 配置贡献者接口
/// 由需要暴露配置编辑能力的插件或模块实现，供配置中心统一收集和展示。
/// </summary>
public interface ISettingsContributor
{
    /// <summary>
    /// 配置分类，用于左侧导航分组，例如：系统、硬件、数据库、报表、网络
    /// </summary>
    string Category { get; }

    /// <summary>
    /// 配置项显示标题
    /// </summary>
    string Title { get; }

    /// <summary>
    /// Material Design 图标名称，可选
    /// </summary>
    string? IconKind { get; }

    /// <summary>
    /// 排序权重，数值越小越靠前
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 配置节路径，用于持久化到 appsettings.json
    /// 例如："Plugins:Configuration:AP.Plugin.Scanner"
    /// </summary>
    string ConfigurationSection { get; }

    /// <summary>
    /// 创建配置编辑 ViewModel
    /// </summary>
    /// <param name="serviceProvider">服务提供器，可解析 IConfiguration、IOptions 等</param>
    /// <returns>实现 <see cref="ISettingsEditorViewModel"/> 的 ViewModel 实例</returns>
    ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider);
}
