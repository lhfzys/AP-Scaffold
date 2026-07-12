using System.Net;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.UI.Base;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace AP.Plugin.Plc.Mitsubishi.Configuration;

/// <summary>
/// 三菱 PLC 配置编辑器
/// </summary>
public partial class MitsubishiPlcConfigurationEditorViewModel : ViewModelBase, ISettingsEditorViewModel
{
    [ObservableProperty] private string _ipAddress = "127.0.0.1";
    [ObservableProperty] private int _port = 6000;
    [ObservableProperty] private int _timeout = 1000;
    [ObservableProperty] private string _version = "Qna_3E";
    [ObservableProperty] private string _heartbeatAddress = "D0.0";

    public bool RequiresRestart => true;

    public MitsubishiPlcConfigurationEditorViewModel(IOptions<MitsubishiPlcOptions> options)
    {
        LoadFromOptions(options.Value);
    }

    public void LoadFromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(MitsubishiPlcOptions.SectionName).Get<MitsubishiPlcOptions>()
                      ?? new MitsubishiPlcOptions();
        LoadFromOptions(options);
    }

    private void LoadFromOptions(MitsubishiPlcOptions options)
    {
        IpAddress = options.IpAddress;
        Port = options.Port;
        Timeout = options.Timeout;
        Version = options.Version;
        HeartbeatAddress = options.HeartbeatAddress;
    }

    public IEnumerable<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(IpAddress))
        {
            errors.Add("IP 地址不能为空");
        }
        else if (!IPAddress.TryParse(IpAddress, out _))
        {
            errors.Add("IP 地址格式不正确");
        }

        if (Port is <= 0 or > 65535)
            errors.Add("端口号必须在 1-65535 之间");

        if (Timeout <= 0)
            errors.Add("超时时间必须大于 0 毫秒");

        if (string.IsNullOrWhiteSpace(Version))
            errors.Add("PLC 版本不能为空");

        if (string.IsNullOrWhiteSpace(HeartbeatAddress))
            errors.Add("心跳地址不能为空");

        return errors;
    }

    public object GetConfigurationValue()
    {
        return new MitsubishiPlcOptions
        {
            IpAddress = IpAddress,
            Port = Port,
            Timeout = Timeout,
            Version = Version,
            HeartbeatAddress = HeartbeatAddress
        };
    }
}
