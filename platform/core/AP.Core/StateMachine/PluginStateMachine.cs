using Microsoft.Extensions.Logging;

namespace AP.Core.StateMachine;

public class PluginStateMachine
{
    private readonly string _pluginId;
    private readonly ILogger _logger;
    private PluginState _currentState;
    private readonly object _lock = new();

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public PluginState CurrentState
    {
        get
        {
            lock (_lock)
            {
                return _currentState;
            }
        }
    }

    public PluginStateMachine(string pluginId, ILogger logger)
    {
        _pluginId = pluginId;
        _logger = logger;
        _currentState = PluginState.Unloaded;
    }

    /// <summary>
    /// 尝试转换状态
    /// </summary>
    public void TransitionTo(PluginState newState)
    {
        lock (_lock)
        {
            if (_currentState == newState) return;

            if (!StateTransitionValidator.CanTransition(_currentState, newState))
            {
                var error = $"非法状态转换: 插件 '{_pluginId}' 试图从 {_currentState} 变为 {newState}";
                _logger.LogError(error);
                throw new InvalidOperationException(error);
            }

            var oldState = _currentState;
            _currentState = newState;

            _logger.LogDebug("插件 '{PluginId}' 状态变更: {OldState} -> {NewState}", _pluginId, oldState, newState);

            try
            {
                StateChanged?.Invoke(this, new StateChangedEventArgs(_pluginId, oldState, newState));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理插件 '{PluginId}' 状态变更事件时发生错误", _pluginId);
            }
        }
    }
}