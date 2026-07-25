using AP.Contracts.Hardware.Models;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using System.Net;

namespace AP.Plugin.SystemSettings.Editors;

/// <summary>
/// PLC 统一配置编辑器。
/// 支持三菱 / 西门子 / 欧姆龙驱动切换。
/// </summary>
public partial class PlcConfigurationEditorViewModel : ViewModelBase, ISettingsEditorViewModel
{
    [ObservableProperty] private string _driverType = "Mitsubishi";
    [ObservableProperty] private string _ipAddress = "127.0.0.1";
    [ObservableProperty] private int _port = 6000;
    [ObservableProperty] private int _timeout = 1000;
    [ObservableProperty] private string _model = "Qna_3E";
    [ObservableProperty] private string _heartbeatAddress = "D0.0";
    [ObservableProperty] private int _heartbeatIntervalSeconds = 2;
    [ObservableProperty] private int _reconnectBackoffSeconds = 5;
    [ObservableProperty] private int _supervisorRestartDelaySeconds = 5;

    public IReadOnlyList<string> DriverTypes { get; } = new[] { "Mitsubishi", "Siemens", "Omron" };

    public bool RequiresRestart => true;

    partial void OnDriverTypeChanged(string value)
    {
        ApplyDriverDefaults(value);
    }

    public PlcConfigurationEditorViewModel()
    {
        ApplyDriverDefaults(DriverType);
    }

    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(PlcOptions.SectionName).Get<PlcOptions>() ?? new PlcOptions();

        // DriverType 先赋值：与当前不同会触发 OnDriverTypeChanged 回填该品牌默认值，
        // 随后保存值逐项覆盖，最终以配置为准（末尾不可再调 ApplyDriverDefaults，会覆盖保存值）
        DriverType = options.DriverType;
        IpAddress = options.IpAddress;
        Port = options.Port;
        Timeout = options.Timeout;
        Model = options.Model;
        HeartbeatAddress = options.HeartbeatAddress;
        HeartbeatIntervalSeconds = options.HeartbeatIntervalSeconds;
        ReconnectBackoffSeconds = options.ReconnectBackoffSeconds;
        SupervisorRestartDelaySeconds = options.SupervisorRestartDelaySeconds;
    }

    /// <summary>
    /// 切换品牌时强制回填该品牌的端口/型号/心跳默认值，
    /// 避免残留其他品牌的参数（心跳地址格式不匹配会导致看门狗误判掉线、反复重连）。
    /// </summary>
    private void ApplyDriverDefaults(string driverType)
    {
        switch (driverType)
        {
            case "Mitsubishi":
                Port = 6000;
                Model = "Qna_3E";
                HeartbeatAddress = "D0.0";
                break;
            case "Siemens":
                Port = 102;
                Model = "S7_1200";
                HeartbeatAddress = "DB1.0.0";
                break;
            case "Omron":
                Port = 9600;
                Model = "FinsTcp";
                HeartbeatAddress = "D0";
                break;
        }
    }

    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(DriverType))
            errors.Add("PLC 驱动类型不能为空");
        else if (!DriverTypes.Contains(DriverType, StringComparer.OrdinalIgnoreCase))
            errors.Add($"不支持的 PLC 驱动类型: {DriverType}");

        if (string.IsNullOrWhiteSpace(IpAddress))
            errors.Add("IP 地址不能为空");
        else if (!IPAddress.TryParse(IpAddress, out _))
            errors.Add("IP 地址格式不正确");

        if (Port is <= 0 or > 65535)
            errors.Add("端口号必须在 1-65535 之间");

        if (Timeout <= 0)
            errors.Add("超时时间必须大于 0 毫秒");

        if (string.IsNullOrWhiteSpace(Model))
            errors.Add("PLC 型号不能为空");

        if (string.IsNullOrWhiteSpace(HeartbeatAddress))
            errors.Add("心跳地址不能为空");

        if (HeartbeatIntervalSeconds < 1)
            errors.Add("心跳周期必须不小于 1 秒");

        if (ReconnectBackoffSeconds < 0)
            errors.Add("重连退避不能为负数");

        if (SupervisorRestartDelaySeconds < 0)
            errors.Add("监督重启延迟不能为负数");

        return errors;
    }

    public object GetConfigurationValue()
    {
        return new PlcOptions
        {
            DriverType = DriverType,
            IpAddress = IpAddress,
            Port = Port,
            Timeout = Timeout,
            Model = Model,
            HeartbeatAddress = HeartbeatAddress,
            HeartbeatIntervalSeconds = HeartbeatIntervalSeconds,
            ReconnectBackoffSeconds = ReconnectBackoffSeconds,
            SupervisorRestartDelaySeconds = SupervisorRestartDelaySeconds
        };
    }
}
