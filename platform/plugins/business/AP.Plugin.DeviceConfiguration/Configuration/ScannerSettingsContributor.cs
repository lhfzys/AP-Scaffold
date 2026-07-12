using AP.Plugin.DeviceConfiguration.Models;
using AP.Plugin.DeviceConfiguration.ViewModels;
using AP.Shared.PluginSDK.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AP.Plugin.DeviceConfiguration.Configuration;

/// <summary>
/// 扫码枪配置贡献者
/// </summary>
public class ScannerSettingsContributor : ISettingsContributor
{
    public string Category => "硬件";
    public string Title => "扫码枪配置";
    public string? IconKind => "BarcodeScanner";
    public int Order => 200;
    public string ConfigurationSection => ScannerConfigModel.SectionName;

    public ISettingsEditorViewModel CreateViewModel(IServiceProvider serviceProvider)
    {
        return ActivatorUtilities.CreateInstance<ScannerSettingsViewModel>(serviceProvider);
    }
}
