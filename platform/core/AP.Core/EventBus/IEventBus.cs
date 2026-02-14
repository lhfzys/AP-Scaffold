using MediatR;

namespace AP.Core.EventBus;

/// <summary>
/// 事件总线抽象接口
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : INotification;
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken ct = default);
}