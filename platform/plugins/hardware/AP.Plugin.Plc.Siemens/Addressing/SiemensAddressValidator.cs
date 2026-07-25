using AP.Contracts.Hardware.DeviceRuntime;

namespace AP.Plugin.Plc.Siemens.Addressing;

/// <summary>
/// 西门子地址验证器（IAddressValidator 薄封装，包装 internal S7Address 解析器）。
/// </summary>
internal sealed class SiemensAddressValidator : IAddressValidator
{
    public string DriverType => "Siemens";

    public bool TryParse(string address, out object? parsedAddress, out string? error)
    {
        if (S7Address.TryParse(address, out var parsed, out _, out error))
        {
            parsedAddress = parsed;
            return true;
        }

        parsedAddress = null;
        return false;
    }
}
