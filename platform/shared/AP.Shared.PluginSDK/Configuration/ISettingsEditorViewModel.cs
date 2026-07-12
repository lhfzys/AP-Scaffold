using System.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace AP.Shared.PluginSDK.Configuration;

/// <summary>
/// 配置编辑器 ViewModel 约定
/// 每个配置贡献者返回的 ViewModel 应实现此接口，以便配置中心统一加载、验证和保存。
/// </summary>
public interface ISettingsEditorViewModel : INotifyPropertyChanged
{
    /// <summary>
    /// 从当前配置刷新 ViewModel 属性
    /// </summary>
    /// <param name="configuration">应用配置</param>
    void LoadFromConfiguration(IConfiguration configuration);

    /// <summary>
    /// 验证当前编辑值
    /// </summary>
    /// <returns>验证错误信息列表；空列表表示验证通过</returns>
    IEnumerable<string> Validate();

    /// <summary>
    /// 将当前编辑值导出为可序列化的配置对象
    /// </summary>
    /// <returns>配置对象</returns>
    object GetConfigurationValue();

    /// <summary>
    /// 是否需要重启应用或重新连接设备才能生效
    /// </summary>
    bool RequiresRestart { get; }
}
