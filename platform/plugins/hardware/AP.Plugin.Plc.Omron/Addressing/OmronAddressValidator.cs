using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Plugin.Plc.Omron.Addressing;

/// <summary>
/// 欧姆龙地址验证器（IAddressValidator 薄封装，包装 internal FinsAddress 解析器）。
/// </summary>
internal sealed class OmronAddressValidator : IAddressValidator
{
    public string DriverType => "Omron";

    public bool TryParse(string address, out object? parsedAddress, out string? error)
    {
        if (FinsAddress.TryParse(address, out var parsed, out _, out error))
        {
            parsedAddress = parsed;
            return true;
        }

        parsedAddress = null;
        return false;
    }
}
