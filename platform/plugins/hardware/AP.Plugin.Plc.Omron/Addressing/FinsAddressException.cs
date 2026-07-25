namespace AP.Plugin.Plc.Omron.Addressing;

/// <summary>
/// 欧姆龙 FINS 地址非法异常（携带结构化错误码，供调用方/测试判断）。
/// </summary>
internal sealed class FinsAddressException : ArgumentException
{
    public FinsAddressException(string address, FinsAddressParseError error, string message)
        : base($"欧姆龙地址非法 '{address}': {message}", nameof(address))
    {
        Error = error;
    }

    /// <summary>结构化解析错误码。</summary>
    public FinsAddressParseError Error { get; }
}
