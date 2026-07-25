namespace AP.Plugin.Plc.Siemens.Addressing;

/// <summary>
/// 西门子 S7 存储区。
/// </summary>
internal enum S7Area
{
    /// <summary>输入映像区。</summary>
    I,

    /// <summary>输出映像区。</summary>
    Q,

    /// <summary>位存储区。</summary>
    M,

    /// <summary>数据块。</summary>
    DB,
}

/// <summary>
/// S7 地址解析错误码（结构化错误，供日志/测试/未来扩展判断；中文消息在 exception message 中）。
/// </summary>
internal enum S7AddressParseError
{
    None = 0,

    /// <summary>地址为 null、空串或全空白。</summary>
    Empty,

    /// <summary>存储区无法识别。</summary>
    UnknownArea,

    /// <summary>DB 号缺失或非法。</summary>
    InvalidDbNumber,

    /// <summary>偏移缺失或含非法字符。</summary>
    InvalidOffset,

    /// <summary>位号非法（非 0-7）。</summary>
    InvalidBitPosition,

    /// <summary>偏移超出允许范围。</summary>
    OffsetOutOfRange,
}

/// <summary>
/// 西门子 S7 地址领域对象（驱动内部，协议相关但不依赖具体通信库类型）。
/// 支持形式：M0.0 / I0.1 / Q0.0 / DB1.0.0 / DB1.2（区 + 偏移 + 可选位号，DB 区带 DB 号）。
/// 提供解析、规范化（统一大写/去前导零）、值相等三能力：
/// 同一地址在系统中始终只有一种标准表示，可直接做缓存键与批量合并的基础。
/// </summary>
internal sealed class S7Address : IEquatable<S7Address>
{
    private const int MaxOffset = 65535;

    private S7Address(S7Area area, int dbNumber, int offset, byte? bit)
    {
        Area = area;
        DbNumber = dbNumber;
        Offset = offset;
        Bit = bit;
    }

    /// <summary>存储区。</summary>
    public S7Area Area { get; }

    /// <summary>DB 号（仅 DB 区有效，其余为 0）。</summary>
    public int DbNumber { get; }

    /// <summary>字节偏移。</summary>
    public int Offset { get; }

    /// <summary>位号（0-7，无位号时为 null）。</summary>
    public byte? Bit { get; }

    /// <summary>是否位地址（如 DB1.0.0、M0.0）。</summary>
    public bool IsBitAddress => Bit.HasValue;

    /// <summary>规范化表示（区域大写 + 去前导零），与 <see cref="ToString"/> 一致。</summary>
    public string Normalized => ToString();

    /// <summary>
    /// 解析地址（不抛异常版）：失败时返回结构化错误码与中文消息。
    /// </summary>
    public static bool TryParse(string? raw, out S7Address address, out S7AddressParseError error, out string? message)
    {
        address = null!;
        error = S7AddressParseError.None;
        message = null;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = S7AddressParseError.Empty;
            message = "地址不能为空";
            return false;
        }

        var s = raw.Trim().ToUpperInvariant();

        // 区域：DB 两字母优先，其次单字母 I/Q/M
        S7Area area;
        string rest;
        if (s.StartsWith("DB"))
        {
            area = S7Area.DB;
            rest = s[2..];
        }
        else if (s.Length >= 1 && char.IsLetter(s[0]) &&
                 Enum.TryParse(s[..1], false, out S7Area oneLetter) &&
                 Enum.IsDefined(oneLetter) && oneLetter != S7Area.DB)
        {
            area = oneLetter;
            rest = s[1..];
        }
        else
        {
            error = S7AddressParseError.UnknownArea;
            message = $"无法识别的存储区（支持 I/Q/M/DB）: '{s}'";
            return false;
        }

        // 按 '.' 拆分：DB 区为 [DB号, 偏移, 位号?]，其余为 [偏移, 位号?]
        var parts = rest.Split('.');
        int dbNumber = 0;
        string offsetPart;
        string? bitPart;

        if (area == S7Area.DB)
        {
            if (parts.Length < 2 || !IsNonNegativeInt(parts[0], out dbNumber) || dbNumber < 1)
            {
                error = S7AddressParseError.InvalidDbNumber;
                message = $"DB 号必须为不小于 1 的整数: '{rest}'";
                return false;
            }

            offsetPart = parts[1];
            bitPart = parts.Length > 2 ? parts[2] : null;
            if (parts.Length > 3)
            {
                error = S7AddressParseError.InvalidBitPosition;
                message = $"地址段过多（DB 区形式为 DB号.偏移[.位号]）: '{s}'";
                return false;
            }
        }
        else
        {
            offsetPart = parts[0];
            bitPart = parts.Length > 1 ? parts[1] : null;
            if (parts.Length > 2)
            {
                error = S7AddressParseError.InvalidBitPosition;
                message = $"地址段过多（形式为 偏移[.位号]）: '{s}'";
                return false;
            }
        }

        // 偏移
        if (!IsNonNegativeInt(offsetPart, out var offset))
        {
            error = S7AddressParseError.InvalidOffset;
            message = $"偏移必须为十进制数字: '{offsetPart}'";
            return false;
        }

        if (offset > MaxOffset)
        {
            error = S7AddressParseError.OffsetOutOfRange;
            message = $"偏移超出允许范围（最大 {MaxOffset}）: '{offsetPart}'";
            return false;
        }

        // 位号：0-7
        byte? bit = null;
        if (bitPart != null)
        {
            if (bitPart.Length != 1 || !char.IsDigit(bitPart[0]) || bitPart[0] > '7')
            {
                error = S7AddressParseError.InvalidBitPosition;
                message = $"位号必须为 0-7: '{bitPart}'";
                return false;
            }

            bit = (byte)(bitPart[0] - '0');
        }

        address = new S7Address(area, dbNumber, offset, bit);
        return true;
    }

    /// <summary>
    /// 解析地址（抛异常版）：非法地址抛 <see cref="S7AddressException"/>。
    /// </summary>
    public static S7Address Parse(string raw)
    {
        if (!TryParse(raw, out var address, out var error, out var message))
            throw new S7AddressException(raw, error, message!);
        return address;
    }

    /// <summary>规范化输出：区域大写 + 去前导零（DB1.0.0 / DB1.2 / M0.0 / I0.1）。</summary>
    public override string ToString()
    {
        var baseText = Area == S7Area.DB ? $"DB{DbNumber}.{Offset}" : $"{Area}{Offset}";
        return Bit.HasValue ? $"{baseText}.{Bit.Value}" : baseText;
    }

    public bool Equals(S7Address? other)
    {
        return other != null && Area == other.Area && DbNumber == other.DbNumber
            && Offset == other.Offset && Bit == other.Bit;
    }

    public override bool Equals(object? obj) => Equals(obj as S7Address);

    public override int GetHashCode() => HashCode.Combine(Area, DbNumber, Offset, Bit);

    private static bool IsNonNegativeInt(string text, out int value)
    {
        value = 0;
        return !string.IsNullOrEmpty(text) && text.All(char.IsDigit) && int.TryParse(text, out value);
    }
}
