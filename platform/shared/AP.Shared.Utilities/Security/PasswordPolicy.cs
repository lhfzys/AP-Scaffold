using System.Security.Cryptography;

namespace AP.Shared.Utilities.Security;

/// <summary>
/// 本地账号密码策略（轻身份模式）。
/// 目标是最小可用：长度 + 字母 + 数字；不强制特殊字符，避免工业现场输入困难。
/// </summary>
public static class PasswordPolicy
{
    public const int DefaultMinLength = 8;

    public static (bool Succeeded, string Message) Validate(string? password, int minLength = DefaultMinLength)
    {
        if (string.IsNullOrWhiteSpace(password))
            return (false, "密码不能为空");

        if (minLength < 1) minLength = DefaultMinLength;
        if (password.Length < minLength)
            return (false, $"密码至少需要 {minLength} 位");

        if (!password.Any(char.IsLetter))
            return (false, "密码至少需要包含一个字母");

        if (!password.Any(char.IsDigit))
            return (false, "密码至少需要包含一个数字");

        return (true, string.Empty);
    }
}

/// <summary>
/// 一次性密码生成器（用于管理员重置密码）。
/// 排除易混淆字符（I/O/0/1），保证字母与数字各至少一个。
/// </summary>
public static class PasswordGenerator
{
    private const string Letters = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
    private const string Digits = "23456789";
    private const string All = Letters + Digits;

    public static string Generate(int length = 12)
    {
        if (length < 8) length = 8;

        var chars = new char[length];
        chars[0] = Letters[RandomNumberGenerator.GetInt32(Letters.Length)];
        chars[1] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];

        for (var i = 2; i < length; i++)
            chars[i] = All[RandomNumberGenerator.GetInt32(All.Length)];

        // Fisher-Yates 洗牌，避免固定“字母+数字”前缀
        for (var i = length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
    }
}
