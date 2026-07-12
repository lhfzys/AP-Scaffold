using AP.Shared.PluginSDK.Configuration;
using AP.Shared.Utilities.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;

namespace AP.Plugin.SystemSettings.Editors;

/// <summary>
/// 应用基础配置编辑器
/// </summary>
public partial class AppConfigurationEditorViewModel : ViewModelBase, ISettingsEditorViewModel
{
    [ObservableProperty] private string _companyName = "未配置公司";
    [ObservableProperty] private string _softwareName = "未配置软件";
    [ObservableProperty] private string _machineId = "Unknown-Machine";
    [ObservableProperty] private string _machineName = "未命名工位";

    public bool RequiresRestart => true;

    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(AppConfigurationOptions.SectionName).Get<AppConfigurationOptions>()
                      ?? new AppConfigurationOptions();

        CompanyName = options.CompanyName;
        SoftwareName = options.SoftwareName;
        MachineId = options.MachineId;
        MachineName = options.MachineName;
    }

    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(CompanyName))
            errors.Add("公司名称不能为空");

        if (string.IsNullOrWhiteSpace(SoftwareName))
            errors.Add("软件名称不能为空");

        if (string.IsNullOrWhiteSpace(MachineId))
            errors.Add("机器编号不能为空");

        if (string.IsNullOrWhiteSpace(MachineName))
            errors.Add("机器名称不能为空");

        return errors;
    }

    public object GetConfigurationValue()
    {
        return new AppConfigurationOptions
        {
            CompanyName = CompanyName,
            SoftwareName = SoftwareName,
            MachineId = MachineId,
            MachineName = MachineName
        };
    }
}
