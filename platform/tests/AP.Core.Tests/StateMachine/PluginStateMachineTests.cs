using AP.Core.StateMachine;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Core.Tests.StateMachine;

public class PluginStateMachineTests
{
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private const string TestPluginId = "AP.Plugin.Test";

    [Fact]
    public void Constructor_InitialStateIsUnloaded()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        sm.CurrentState.Should().Be(PluginState.Unloaded);
    }

    [Fact]
    public void TransitionTo_ValidTransition_ChangesState()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        sm.TransitionTo(PluginState.Discovered);
        sm.CurrentState.Should().Be(PluginState.Discovered);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_ThrowsInvalidOperationException()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        var act = () => sm.TransitionTo(PluginState.Running);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*非法状态转换*");
    }

    [Fact]
    public void TransitionTo_SameState_DoesNotChangeState()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        sm.TransitionTo(PluginState.Discovered);
        sm.CurrentState.Should().Be(PluginState.Discovered);

        // Transition to same state should be no-op
        sm.TransitionTo(PluginState.Discovered);
        sm.CurrentState.Should().Be(PluginState.Discovered);
    }

    [Fact]
    public void TransitionTo_TriggersStateChangedEvent()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        PluginState? oldState = null;
        PluginState? newState = null;
        string? triggeredPluginId = null;

        sm.StateChanged += (sender, args) =>
        {
            oldState = args.OldState;
            newState = args.NewState;
            triggeredPluginId = args.PluginId;
        };

        sm.TransitionTo(PluginState.Discovered);

        oldState.Should().Be(PluginState.Unloaded);
        newState.Should().Be(PluginState.Discovered);
        triggeredPluginId.Should().Be(TestPluginId);
    }

    [Fact]
    public void TransitionTo_InvalidTransition_DoesNotTriggerStateChanged()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        var eventTriggered = false;

        sm.StateChanged += (sender, args) => eventTriggered = true;

        try { sm.TransitionTo(PluginState.Running); } catch { }

        eventTriggered.Should().BeFalse();
    }

    [Fact]
    public void FullLifecycle_SuccessfulTransitions_AllStatesReached()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);

        sm.TransitionTo(PluginState.Discovered);
        sm.CurrentState.Should().Be(PluginState.Discovered);

        sm.TransitionTo(PluginState.Loading);
        sm.CurrentState.Should().Be(PluginState.Loading);

        sm.TransitionTo(PluginState.Loaded);
        sm.CurrentState.Should().Be(PluginState.Loaded);

        sm.TransitionTo(PluginState.Initializing);
        sm.CurrentState.Should().Be(PluginState.Initializing);

        sm.TransitionTo(PluginState.Initialized);
        sm.CurrentState.Should().Be(PluginState.Initialized);

        sm.TransitionTo(PluginState.Starting);
        sm.CurrentState.Should().Be(PluginState.Starting);

        sm.TransitionTo(PluginState.Running);
        sm.CurrentState.Should().Be(PluginState.Running);

        sm.TransitionTo(PluginState.Stopping);
        sm.CurrentState.Should().Be(PluginState.Stopping);

        sm.TransitionTo(PluginState.Stopped);
        sm.CurrentState.Should().Be(PluginState.Stopped);
    }

    [Fact]
    public void ErrorLifecycle_TransitionToFailed_StateChangedCorrectly()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);

        sm.TransitionTo(PluginState.Discovered);
        sm.TransitionTo(PluginState.Loading);
        sm.TransitionTo(PluginState.Loaded);
        sm.TransitionTo(PluginState.Initializing);

        // Simulate initialization failure
        sm.TransitionTo(PluginState.Failed);
        sm.CurrentState.Should().Be(PluginState.Failed);
    }

    [Fact]
    public void FailedState_CanResetToUnloaded()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);

        sm.TransitionTo(PluginState.Discovered);
        sm.TransitionTo(PluginState.Loading);
        sm.TransitionTo(PluginState.Failed);

        sm.TransitionTo(PluginState.Unloaded);
        sm.CurrentState.Should().Be(PluginState.Unloaded);
    }

    [Fact]
    public void ThreadSafety_MultipleTransitions_StateConsistent()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        var exceptions = new List<Exception>();

        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            tasks[i] = Task.Run(() =>
            {
                try
                {
                    sm.TransitionTo(PluginState.Discovered);
                }
                catch (Exception ex)
                {
                    lock (exceptions) { exceptions.Add(ex); }
                }
            });
        }

        Task.WaitAll(tasks);

        // Multiple threads may race; only one should succeed the transition
        // and others should get InvalidOperationException or be no-ops
        sm.CurrentState.Should().Be(PluginState.Discovered);
    }

    [Fact]
    public void GetCurrentState_ReturnsLatestState()
    {
        var sm = new PluginStateMachine(TestPluginId, _logger);
        sm.CurrentState.Should().Be(PluginState.Unloaded);

        sm.TransitionTo(PluginState.Discovered);
        sm.CurrentState.Should().Be(PluginState.Discovered);

        sm.TransitionTo(PluginState.Loading);
        sm.CurrentState.Should().Be(PluginState.Loading);
    }
}