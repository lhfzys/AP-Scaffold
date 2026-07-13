using AP.Core.StateMachine;
using FluentAssertions;
using Xunit;

namespace AP.Core.Tests.StateMachine;

public class StateTransitionValidatorTests
{
    [Theory]
    [InlineData(PluginState.Unloaded, PluginState.Discovered)]
    [InlineData(PluginState.Discovered, PluginState.Loading)]
    [InlineData(PluginState.Discovered, PluginState.Unloaded)]
    [InlineData(PluginState.Discovered, PluginState.Failed)]
    [InlineData(PluginState.Loading, PluginState.Loaded)]
    [InlineData(PluginState.Loading, PluginState.Failed)]
    [InlineData(PluginState.Loaded, PluginState.Initializing)]
    [InlineData(PluginState.Loaded, PluginState.Unloaded)]
    [InlineData(PluginState.Loaded, PluginState.Failed)]
    [InlineData(PluginState.Initializing, PluginState.Initialized)]
    [InlineData(PluginState.Initializing, PluginState.Failed)]
    [InlineData(PluginState.Initialized, PluginState.Starting)]
    [InlineData(PluginState.Initialized, PluginState.Unloaded)]
    [InlineData(PluginState.Initialized, PluginState.Failed)]
    [InlineData(PluginState.Starting, PluginState.Running)]
    [InlineData(PluginState.Starting, PluginState.Failed)]
    [InlineData(PluginState.Starting, PluginState.Stopped)]
    [InlineData(PluginState.Running, PluginState.Stopping)]
    [InlineData(PluginState.Running, PluginState.Degraded)]
    [InlineData(PluginState.Running, PluginState.Failed)]
    [InlineData(PluginState.Degraded, PluginState.Stopping)]
    [InlineData(PluginState.Degraded, PluginState.Running)]
    [InlineData(PluginState.Degraded, PluginState.Failed)]
    [InlineData(PluginState.Stopping, PluginState.Stopped)]
    [InlineData(PluginState.Stopping, PluginState.Failed)]
    [InlineData(PluginState.Stopped, PluginState.Starting)]
    [InlineData(PluginState.Stopped, PluginState.Unloaded)]
    [InlineData(PluginState.Stopped, PluginState.Failed)]
    [InlineData(PluginState.Failed, PluginState.Unloaded)]
    [InlineData(PluginState.Failed, PluginState.Discovered)]
    public void CanTransition_ValidTransitions_ReturnsTrue(PluginState current, PluginState target)
    {
        var result = StateTransitionValidator.CanTransition(current, target);
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(PluginState.Unloaded, PluginState.Running)]
    [InlineData(PluginState.Unloaded, PluginState.Failed)]
    [InlineData(PluginState.Loading, PluginState.Running)]
    [InlineData(PluginState.Initialized, PluginState.Running)]
    [InlineData(PluginState.Running, PluginState.Unloaded)]
    [InlineData(PluginState.Running, PluginState.Loaded)]
    [InlineData(PluginState.Stopped, PluginState.Running)]
    [InlineData(PluginState.Failed, PluginState.Running)]
    [InlineData(PluginState.Failed, PluginState.Loaded)]
    [InlineData(PluginState.Frozen, PluginState.Running)]
    [InlineData(PluginState.Deprecated, PluginState.Running)]
    public void CanTransition_InvalidTransitions_ReturnsFalse(PluginState current, PluginState target)
    {
        var result = StateTransitionValidator.CanTransition(current, target);
        result.Should().BeFalse();
    }

    [Fact]
    public void CanTransition_SameState_ReturnsTrue()
    {
        foreach (PluginState state in Enum.GetValues<PluginState>())
        {
            StateTransitionValidator.CanTransition(state, state).Should().BeTrue();
        }
    }

    [Fact]
    public void AllPluginStates_HaveDefinedTransitions()
    {
        // All states defined in the enum should be in the _allowedTransitions dictionary
        // (directly or via the same-state fallback)
        var allStates = Enum.GetValues<PluginState>();

        foreach (var state in allStates)
        {
            // At minimum, transition to itself should work (handled by CanTransition method)
            StateTransitionValidator.CanTransition(state, state).Should().BeTrue(
                $"State {state} should at least support transition to itself");
        }
    }

    [Fact]
    public void FullLifecycle_SuccessfulPath_AllTransitionsValid()
    {
        // Simulate a full successful plugin lifecycle
        var transitions = new[]
        {
            (PluginState.Unloaded, PluginState.Discovered),
            (PluginState.Discovered, PluginState.Loading),
            (PluginState.Loading, PluginState.Loaded),
            (PluginState.Loaded, PluginState.Initializing),
            (PluginState.Initializing, PluginState.Initialized),
            (PluginState.Initialized, PluginState.Starting),
            (PluginState.Starting, PluginState.Running),
            (PluginState.Running, PluginState.Stopping),
            (PluginState.Stopping, PluginState.Stopped),
        };

        foreach (var (current, target) in transitions)
        {
            StateTransitionValidator.CanTransition(current, target).Should().BeTrue(
                $"Transition {current} -> {target} should be valid");
        }
    }

    [Fact]
    public void FailedState_ResetPath_TransitionsValid()
    {
        StateTransitionValidator.CanTransition(PluginState.Failed, PluginState.Unloaded).Should().BeTrue();
        StateTransitionValidator.CanTransition(PluginState.Failed, PluginState.Discovered).Should().BeTrue();
    }
}