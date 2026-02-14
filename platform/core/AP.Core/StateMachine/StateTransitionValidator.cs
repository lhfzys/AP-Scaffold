namespace AP.Core.StateMachine;

/// <summary>
/// 验证插件状态转换是否合法
/// </summary>
public class StateTransitionValidator
{
    private static readonly Dictionary<PluginState, HashSet<PluginState>> _allowedTransitions = new()
    {
        { PluginState.Unloaded, new HashSet<PluginState> { PluginState.Discovered } },
        {
            PluginState.Discovered,
            [PluginState.Loading, PluginState.Unloaded, PluginState.Failed]
        },
        { PluginState.Loading, new HashSet<PluginState> { PluginState.Loaded, PluginState.Failed } },
        {
            PluginState.Loaded,
            [PluginState.Initializing, PluginState.Unloaded, PluginState.Failed]
        },
        { PluginState.Initializing, new HashSet<PluginState> { PluginState.Initialized, PluginState.Failed } },
        {
            PluginState.Initialized,
            [PluginState.Starting, PluginState.Unloaded, PluginState.Failed]
        },
        {
            PluginState.Starting,
            [PluginState.Running, PluginState.Failed, PluginState.Stopped]
        },
        {
            PluginState.Running,
            [PluginState.Stopping, PluginState.Degraded, PluginState.Failed]
        },
        {
            PluginState.Degraded,
            [PluginState.Stopping, PluginState.Running, PluginState.Failed]
        },
        { PluginState.Stopping, new HashSet<PluginState> { PluginState.Stopped, PluginState.Failed } },
        {
            PluginState.Stopped,
            [PluginState.Starting, PluginState.Unloaded, PluginState.Failed]
        },
        { PluginState.Failed, new HashSet<PluginState> { PluginState.Unloaded, PluginState.Discovered } } // 允许重置
    };

    public static bool CanTransition(PluginState current, PluginState target)
    {
        if (current == target) return true;
        return _allowedTransitions.ContainsKey(current) && _allowedTransitions[current].Contains(target);
    }
}