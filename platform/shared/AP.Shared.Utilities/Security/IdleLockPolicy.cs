namespace AP.Shared.Utilities.Security;

/// <summary>
/// 空闲锁屏策略（纯逻辑，便于单元测试）。
/// </summary>
public static class IdleLockPolicy
{
    /// <summary>是否应触发锁屏；timeoutMinutes &lt;= 0 表示关闭。</summary>
    public static bool ShouldLock(TimeSpan idleTime, int timeoutMinutes)
    {
        return timeoutMinutes > 0 && idleTime >= TimeSpan.FromMinutes(timeoutMinutes);
    }
}
