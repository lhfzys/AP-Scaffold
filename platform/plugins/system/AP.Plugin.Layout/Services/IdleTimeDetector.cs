using System.Runtime.InteropServices;

namespace AP.Plugin.Layout.Services;

/// <summary>
/// 读取 Windows 系统级空闲时间（键盘/鼠标最后一次输入至今）。
/// </summary>
internal static class IdleTimeDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo plii);

    /// <summary>获取空闲时长；读取失败时返回 Zero（宁可不锁屏，也不能误锁）。</summary>
    public static TimeSpan GetIdleTime()
    {
        var info = new LastInputInfo { cbSize = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;

        var idleMilliseconds = unchecked((uint)Environment.TickCount - info.dwTime);
        return TimeSpan.FromMilliseconds(idleMilliseconds);
    }
}
