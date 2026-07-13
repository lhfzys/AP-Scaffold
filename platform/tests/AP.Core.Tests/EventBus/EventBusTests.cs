using AP.Core.EventBus;
using FluentAssertions;
using MediatR;
using NSubstitute;
using Xunit;

namespace AP.Core.Tests.EventBus;

public class EventBusTests
{
    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly MediatREventBus _eventBus;

    public EventBusTests()
    {
        _eventBus = new MediatREventBus(_mediator);
    }

    [Fact]
    public async Task PublishAsync_WhenCalled_PublishesToMediator()
    {
        var notification = new TestNotification();

        await _eventBus.PublishAsync(notification);

        await _mediator.Received(1).Publish(notification, default);
    }

    [Fact]
    public async Task PublishAsync_WithCancellationToken_PassesTokenToMediator()
    {
        var notification = new TestNotification();
        var cts = new CancellationTokenSource();

        await _eventBus.PublishAsync(notification, cts.Token);

        await _mediator.Received(1).Publish(notification, cts.Token);
    }

    [Fact]
    public async Task SendAsync_WhenCalled_SendsToMediator()
    {
        var request = new TestRequest();
        _mediator.Send(request, default).Returns(Task.FromResult("test-response"));

        var result = await _eventBus.SendAsync(request);

        result.Should().Be("test-response");
        await _mediator.Received(1).Send(request, default);
    }

    [Fact]
    public async Task SendAsync_WithCancellationToken_PassesTokenToMediator()
    {
        var request = new TestRequest();
        var cts = new CancellationTokenSource();
        _mediator.Send(request, cts.Token).Returns(Task.FromResult("response"));

        await _eventBus.SendAsync(request, cts.Token);

        await _mediator.Received(1).Send(request, cts.Token);
    }

    [Fact]
    public void IEventBus_CanBeImplemented()
    {
        // Verify the interface is accessible
        typeof(IEventBus).IsInterface.Should().BeTrue();
        typeof(IEventBus).GetMethods().Should().HaveCount(2);
    }

    [Fact]
    public async Task SendAsync_GenericRequest_ReturnsCorrectResponseType()
    {
        var request = new TestRequest();

        _mediator.Send(request, default).Returns(Task.FromResult("response-data"));

        var result = await _eventBus.SendAsync(request);

        result.Should().BeOfType<string>();
        result.Should().Be("response-data");
    }
}

public class TestNotification : INotification { }

public class TestRequest : IRequest<string> { }