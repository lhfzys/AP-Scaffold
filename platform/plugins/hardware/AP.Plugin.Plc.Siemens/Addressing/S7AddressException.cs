namespace AP.Plugin.Plc.Siemens.Addressing;

/// <summary>
/// 西门子 S7 地址非法异常（携带结构化错误码，供调用方/测试判断）。
/// </summary>
internal sealed class S7AddressException : ArgumentException
{
    public S7AddressException(string address, S7AddressParseError error, string message)
        : base($"西门子地址非法 '{address}': {message}", nameof(address))
    {
        Error = error;
    }

    /// <summary>结构化解析错误码。</summary>
    public S7AddressParseError Error { get; }
}
