namespace AP.Plugin.Plc.Mitsubishi.Addressing;

/// <summary>
/// 三菱 MC 协议软元件区。
/// </summary>
internal enum MitsubishiArea
{
    X, Y, M, L, F, V, S, B, D, W, R, ZR, SM, SD
}

/// <summary>
/// 地址解析错误码（结构化错误，供日志/测试/未来扩展判断；中文消息在 exception message 中）。
/// </summary>
internal enum AddressParseError
{
    None = 0,

    /// <summary>地址为 null、空串或全空白。</summary>
    Empty,

    /// <summary>软元件区无法识别。</summary>
    UnknownArea,

    /// <summary>偏移部分缺失或含非法字符。</summary>
    InvalidOffset,

    /// <summary>位号非法（非 0-F、位号用于位元件区、或格式错误）。</summary>
    InvalidBitPosition,

    /// <summary>偏移超出允许范围。</summary>
    OffsetOutOfRange,
}

/// <summary>
/// 三菱 MC 地址领域对象（驱动内部，协议相关但不依赖具体通信库类型）。
/// 提供解析、规范化（统一大写/去前导零）、值相等三能力：
/// 同一地址在系统中始终只有一种标准表示，可直接做缓存键与批量合并的基础。
/// X/Y/B/W 按三菱惯例为十六进制偏移，其余为十进制；位地址（如 D100.0）仅允许字元件区。
/// </summary>
internal sealed class McAddress : IEquatable<McAddress>
{
    private static readonly HashSet<MitsubishiArea> HexOffsetAreas = [MitsubishiArea.X, MitsubishiArea.Y, MitsubishiArea.B, MitsubishiArea.W];
    private static readonly HashSet<MitsubishiArea> WordAreas = [MitsubishiArea.D, MitsubishiArea.W, MitsubishiArea.R, MitsubishiArea.ZR, MitsubishiArea.SD];
    private const int MaxOffset = 0xFFFFFF;

    private McAddress(MitsubishiArea area, int offset, byte? bit)
    {
        Area = area;
        Offset = offset;
        Bit = bit;
    }

    /// <summary>软元件区。</summary>
    public MitsubishiArea Area { get; }

    /// <summary>偏移（X/Y/B/W 为十六进制值，其余为十进制值）。</summary>
    public int Offset { get; }

    /// <summary>位号（0-F，仅字元件区可有；无位号时为 null）。</summary>
    public byte? Bit { get; }

    /// <summary>是否位地址（如 D100.0）。</summary>
    public bool IsBitAddress => Bit.HasValue;

    /// <summary>规范化表示（区域大写 + 偏移去前导零 + 位号大写），与 <see cref="ToString"/> 一致。</summary>
    public string Normalized => ToString();

    /// <summary>
    /// 解析地址（不抛异常版）：失败时返回结构化错误码与中文消息。
    /// </summary>
    public static bool TryParse(string? raw, out McAddress address, out AddressParseError error, out string? message)
    {
        address = null!;
        error = AddressParseError.None;
        message = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = AddressParseError.Empty;
            message = "地址不能为空";
            return false;
        }

        var s = raw.Trim();

        // 区域：先尝试两字母区（ZR/SM/SD），再单字母区；首字符必须是字母
        MitsubishiArea area;
        int areaLength;
        if (s.Length >= 2 && char.IsLetter(s[0]) && char.IsLetter(s[1]) &&
            Enum.TryParse(s[..2].ToUpperInvariant(), false, out MitsubishiArea twoLetter) &&
            Enum.IsDefined(twoLetter) && IsTwoLetterArea(twoLetter))
        {
            area = twoLetter;
            areaLength = 2;
        }
        else if (char.IsLetter(s[0]) &&
                 Enum.TryParse(s[..1].ToUpperInvariant(), false, out MitsubishiArea oneLetter) &&
                 Enum.IsDefined(oneLetter) && !IsTwoLetterArea(oneLetter))
        {
            area = oneLetter;
            areaLength = 1;
        }
        else
        {
            error = AddressParseError.UnknownArea;
            message = $"无法识别的软元件区（支持 X/Y/M/L/F/V/S/B/D/W/R/ZR/SM/SD）: '{s}'";
            return false;
        }

        // 拆分偏移与位号
        var rest = s[areaLength..];
        var dotIndex = rest.IndexOf('.');
        var offsetPart = dotIndex < 0 ? rest : rest[..dotIndex];
        var bitPart = dotIndex < 0 ? null : rest[(dotIndex + 1)..];

        // 偏移：X/Y/B/W 十六进制，其余十进制
        var isHex = HexOffsetAreas.Contains(area);
        if (string.IsNullOrEmpty(offsetPart) ||
            !(isHex ? offsetPart.All(Uri.IsHexDigit) : offsetPart.All(char.IsDigit)))
        {
            error = AddressParseError.InvalidOffset;
            message = isHex
                ? $"偏移必须为十六进制（0-9A-F）: '{offsetPart}'"
                : $"偏移必须为十进制数字: '{offsetPart}'";
            return false;
        }

        int offset;
        try
        {
            offset = isHex
                ? Convert.ToInt32(offsetPart, 16)
                : int.Parse(offsetPart);
        }
        catch (OverflowException)
        {
            error = AddressParseError.OffsetOutOfRange;
            message = $"偏移超出允许范围（最大 {MaxOffset}）: '{offsetPart}'";
            return false;
        }

        if (offset > MaxOffset)
        {
            error = AddressParseError.OffsetOutOfRange;
            message = $"偏移超出允许范围（最大 {MaxOffset}）: '{offsetPart}'";
            return false;
        }

        // 位号：仅字元件区允许，且必须为单个十六进制位（0-F）
        byte? bit = null;
        if (bitPart != null)
        {
            if (!WordAreas.Contains(area))
            {
                error = AddressParseError.InvalidBitPosition;
                message = $"位号仅允许字元件区（D/W/R/ZR/SD），'{area}' 为位元件区";
                return false;
            }

            if (bitPart.Length != 1 || !Uri.IsHexDigit(bitPart[0]))
            {
                error = AddressParseError.InvalidBitPosition;
                message = $"位号必须为 0-F 的单个十六进制位: '{bitPart}'";
                return false;
            }

            bit = Convert.ToByte(bitPart, 16);
        }

        address = new McAddress(area, offset, bit);
        return true;
    }

    /// <summary>
    /// 解析地址（抛异常版）：非法地址抛 <see cref="MitsubishiAddressException"/>。
    /// </summary>
    public static McAddress Parse(string raw)
    {
        if (!TryParse(raw, out var address, out var error, out var message))
            throw new MitsubishiAddressException(raw, error, message!);
        return address;
    }

    /// <summary>规范化输出：区域大写 + 偏移去前导零（十六进制区大写）+ 位号大写。</summary>
    public override string ToString()
    {
        var offsetText = HexOffsetAreas.Contains(Area) ? Offset.ToString("X") : Offset.ToString();
        return Bit.HasValue ? $"{Area}{offsetText}.{Bit.Value:X}" : $"{Area}{offsetText}";
    }

    public bool Equals(McAddress? other)
    {
        return other != null && Area == other.Area && Offset == other.Offset && Bit == other.Bit;
    }

    public override bool Equals(object? obj) => Equals(obj as McAddress);

    public override int GetHashCode() => HashCode.Combine(Area, Offset, Bit);

    private static bool IsTwoLetterArea(MitsubishiArea area) =>
        area is MitsubishiArea.ZR or MitsubishiArea.SM or MitsubishiArea.SD;
}
