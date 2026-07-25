namespace AP.Plugin.Plc.Omron.Addressing;

/// <summary>
/// 欧姆龙 FINS 存储区。
/// </summary>
internal enum FinsArea
{
    /// <summary>数据存储区（DM）。</summary>
    D,

    /// <summary>通道 I/O 区（CIO）。</summary>
    C,

    /// <summary>工作区（WR）。</summary>
    W,

    /// <summary>保持区（HR）。</summary>
    H,

    /// <summary>辅助区（AR）。</summary>
    A,

    /// <summary>扩展数据存储区（EM，带存储体号）。</summary>
    E,
}

/// <summary>
/// FINS 地址解析错误码（结构化错误，供日志/测试/未来扩展判断；中文消息在 exception message 中）。
/// </summary>
internal enum FinsAddressParseError
{
    None = 0,

    /// <summary>地址为 null、空串或全空白。</summary>
    Empty,

    /// <summary>存储区无法识别。</summary>
    UnknownArea,

    /// <summary>E 区存储体号缺失或非法。</summary>
    InvalidBankNumber,

    /// <summary>字偏移缺失或含非法字符。</summary>
    InvalidOffset,

    /// <summary>位号非法（非 0-15）。</summary>
    InvalidBitPosition,

    /// <summary>偏移超出允许范围。</summary>
    OffsetOutOfRange,
}

/// <summary>
/// 欧姆龙 FINS 地址领域对象（驱动内部，协议相关但不依赖具体通信库类型）。
/// 支持形式：D100 / D100.0 / C0.0 / E0.100 / E0.100.0（区 + 字偏移 + 可选位号，E 区带存储体号）。
/// 提供解析、规范化（统一大写/去前导零）、值相等三能力：
/// 同一地址在系统中始终只有一种标准表示，可直接做缓存键与批量合并的基础。
/// </summary>
internal sealed class FinsAddress : IEquatable<FinsAddress>
{
    private const int MaxOffset = 65535;
    private const int MaxBank = 0xF;

    private FinsAddress(FinsArea area, int bank, int offset, byte? bit)
    {
        Area = area;
        Bank = bank;
        Offset = offset;
        Bit = bit;
    }

    /// <summary>存储区。</summary>
    public FinsArea Area { get; }

    /// <summary>E 区存储体号（仅 E 区有效，其余为 0）。</summary>
    public int Bank { get; }

    /// <summary>字偏移。</summary>
    public int Offset { get; }

    /// <summary>位号（0-15，无位号时为 null）。</summary>
    public byte? Bit { get; }

    /// <summary>是否位地址（如 D100.0、C0.0）。</summary>
    public bool IsBitAddress => Bit.HasValue;

    /// <summary>规范化表示（区域大写 + 去前导零），与 <see cref="ToString"/> 一致。</summary>
    public string Normalized => ToString();

    /// <summary>
    /// 解析地址（不抛异常版）：失败时返回结构化错误码与中文消息。
    /// </summary>
    public static bool TryParse(string? raw, out FinsAddress address, out FinsAddressParseError error, out string? message)
    {
        address = null!;
        error = FinsAddressParseError.None;
        message = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = FinsAddressParseError.Empty;
            message = "地址不能为空";
            return false;
        }

        var s = raw.Trim().ToUpperInvariant();

        // 区域：单字母 D/C/W/H/A/E
        if (!char.IsLetter(s[0]) ||
            !Enum.TryParse(s[..1], false, out FinsArea area) ||
            !Enum.IsDefined(area))
        {
            error = FinsAddressParseError.UnknownArea;
            message = $"无法识别的存储区（支持 D/C/W/H/A/E）: '{s}'";
            return false;
        }

        // 按 '.' 拆分：E 区为 [体号, 偏移, 位号?]，其余为 [偏移, 位号?]
        var parts = s[1..].Split('.');
        int bank = 0;
        string offsetPart;
        string? bitPart;

        if (area == FinsArea.E)
        {
            if (parts.Length < 2 || !IsNonNegativeInt(parts[0], out bank) || bank > MaxBank)
            {
                error = FinsAddressParseError.InvalidBankNumber;
                message = $"E 区存储体号必须为 0-{MaxBank} 的整数: '{s[1..]}'";
                return false;
            }

            offsetPart = parts[1];
            bitPart = parts.Length > 2 ? parts[2] : null;
            if (parts.Length > 3)
            {
                error = FinsAddressParseError.InvalidBitPosition;
                message = $"地址段过多（E 区形式为 体号.偏移[.位号]）: '{s}'";
                return false;
            }
        }
        else
        {
            offsetPart = parts[0];
            bitPart = parts.Length > 1 ? parts[1] : null;
            if (parts.Length > 2)
            {
                error = FinsAddressParseError.InvalidBitPosition;
                message = $"地址段过多（形式为 偏移[.位号]）: '{s}'";
                return false;
            }
        }

        // 偏移
        if (!IsNonNegativeInt(offsetPart, out var offset))
        {
            error = FinsAddressParseError.InvalidOffset;
            message = $"偏移必须为十进制数字: '{offsetPart}'";
            return false;
        }

        if (offset > MaxOffset)
        {
            error = FinsAddressParseError.OffsetOutOfRange;
            message = $"偏移超出允许范围（最大 {MaxOffset}）: '{offsetPart}'";
            return false;
        }

        // 位号：0-15
        byte? bit = null;
        if (bitPart != null)
        {
            if (!IsNonNegativeInt(bitPart, out var bitValue) || bitValue > 15)
            {
                error = FinsAddressParseError.InvalidBitPosition;
                message = $"位号必须为 0-15: '{bitPart}'";
                return false;
            }

            bit = (byte)bitValue;
        }

        address = new FinsAddress(area, bank, offset, bit);
        return true;
    }

    /// <summary>
    /// 解析地址（抛异常版）：非法地址抛 <see cref="FinsAddressException"/>。
    /// </summary>
    public static FinsAddress Parse(string raw)
    {
        if (!TryParse(raw, out var address, out var error, out var message))
            throw new FinsAddressException(raw, error, message!);
        return address;
    }

    /// <summary>规范化输出：区域大写 + 去前导零（D100 / D100.0 / E0.100 / E0.100.0）。</summary>
    public override string ToString()
    {
        var baseText = Area == FinsArea.E ? $"E{Bank}.{Offset}" : $"{Area}{Offset}";
        return Bit.HasValue ? $"{baseText}.{Bit.Value}" : baseText;
    }

    public bool Equals(FinsAddress? other)
    {
        return other != null && Area == other.Area && Bank == other.Bank
            && Offset == other.Offset && Bit == other.Bit;
    }

    public override bool Equals(object? obj) => Equals(obj as FinsAddress);

    public override int GetHashCode() => HashCode.Combine(Area, Bank, Offset, Bit);

    private static bool IsNonNegativeInt(string text, out int value)
    {
        value = 0;
        return !string.IsNullOrEmpty(text) && text.All(char.IsDigit) && int.TryParse(text, out value);
    }
}
