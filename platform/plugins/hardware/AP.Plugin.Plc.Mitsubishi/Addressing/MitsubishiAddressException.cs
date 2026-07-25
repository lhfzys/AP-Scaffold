namespace AP.Plugin.Plc.Mitsubishi.Addressing;

/// <summary>
/// 三菱地址非法异常（携带结构化错误码，供调用方/测试判断）。
/// </summary>
internal sealed class MitsubishiAddressException : ArgumentException
{
    public MitsubishiAddressException(string address, AddressParseError error, string message)
        : base($"三菱地址非法 '{address}': {message}", nameof(address))
    {
        Error = error;
    }

    /// <summary>结构化解析错误码。</summary>
    public AddressParseError Error { get; }
}
