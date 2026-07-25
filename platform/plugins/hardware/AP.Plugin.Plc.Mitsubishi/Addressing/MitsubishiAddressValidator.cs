using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Plugin.Plc.Mitsubishi.Addressing;

/// <summary>
/// 三菱地址验证器（IAddressValidator 薄封装，包装 internal McAddress 解析器）。
/// </summary>
internal sealed class MitsubishiAddressValidator : IAddressValidator
{
    public string DriverType => "Mitsubishi";

    public bool TryParse(string address, out object? parsedAddress, out string? error)
    {
        if (McAddress.TryParse(address, out var parsed, out _, out error))
        {
            parsedAddress = parsed;
            return true;
        }

        parsedAddress = null;
        return false;
    }
}
