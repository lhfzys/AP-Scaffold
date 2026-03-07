namespace AP.Contracts.Hardware.Models;

/// <summary>
///     无装箱开销的 PLC 数据值结构体
/// </summary>
public readonly struct PlcValue
{
    // 0: Boolean, 1: Integer, 2: Float/Double, 3: String
    private readonly byte _typeCode;

    // 复用同一个 8 字节空间存储数字类型 (double 可无损存储 int/float/short/bool)
    private readonly double _numericValue;

    // 引用类型单独存放 (大多数 PLC 数据不是字符串，这个字段多数情况为 null)
    private readonly string? _stringValue;

    private PlcValue(byte typeCode, double numericValue, string? stringValue)
    {
        _typeCode = typeCode;
        _numericValue = numericValue;
        _stringValue = stringValue;
    }

    // 隐式转换：赋值时自动封箱为结构体，完全在栈(Stack)上分配，零 GC 压力
    public static implicit operator PlcValue(bool b)
    {
        return new PlcValue(0, b ? 1 : 0, null);
    }

    public static implicit operator PlcValue(short s)
    {
        return new PlcValue(1, s, null);
    }

    public static implicit operator PlcValue(int i)
    {
        return new PlcValue(1, i, null);
    }

    public static implicit operator PlcValue(float f)
    {
        return new PlcValue(2, f, null);
    }

    public static implicit operator PlcValue(double d)
    {
        return new PlcValue(2, d, null);
    }

    public static implicit operator PlcValue(string s)
    {
        return new PlcValue(3, 0, s);
    }

    // 格式化输出，gRPC 和 日志 直接调用它
    public override string ToString()
    {
        return _typeCode switch
        {
            0 => _numericValue > 0 ? "true" : "false",
            1 => ((int)_numericValue).ToString(),
            2 => _numericValue.ToString("R"), // "R" 保证浮点数精度
            3 => _stringValue ?? string.Empty,
            _ => string.Empty
        };
    }

    // 如果业务层确实需要取回 object
    public object GetRawValue()
    {
        return _typeCode switch
        {
            0 => _numericValue > 0,
            1 => (int)_numericValue,
            2 => (float)_numericValue, // 按需调整
            3 => _stringValue ?? string.Empty,
            _ => throw new InvalidOperationException("未知的 PLC 类型")
        };
    }
}