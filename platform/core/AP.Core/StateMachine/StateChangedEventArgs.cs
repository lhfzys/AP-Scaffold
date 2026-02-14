namespace AP.Core.StateMachine;

public class StateChangedEventArgs(string pluginId, PluginState oldState, PluginState newState)
    : EventArgs
{
    public PluginState OldState { get; } = oldState;
    public PluginState NewState { get; } = newState;
    public string PluginId { get; } = pluginId;
}