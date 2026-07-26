using AP.Contracts.Hardware.DeviceRuntime;
using MediatR;

namespace AP.Infra.Hardware.DeviceRuntime;

/// <summary>
/// Tag 值变化发布器：订阅采集引擎的 TagPolled 钩子，仅变化（Changed=true）时
/// 经 MediatR 发布 <see cref="TagValueChangedEvent"/>（变化才通知）。
/// 由宿主在引擎启动后 Attach。
/// </summary>
public sealed class TagValueChangedPublisher
{
    private readonly IMediator _mediator;

    public TagValueChangedPublisher(IMediator mediator)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    }

    /// <summary>
    /// 附加到采集引擎；返回的 <see cref="IDisposable"/> 用于退订。
    /// 发布为火忘（失败被观察、不影响采集循环）。
    /// </summary>
    public IDisposable Attach(TagAcquisitionEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);

        EventHandler<TagPolledEventArgs> handler = (_, args) =>
        {
            if (!args.Changed) return;
            _ = _mediator.Publish(new TagValueChangedEvent(args.Name, args.Value))
                .ContinueWith(
                    t => { _ = t.Exception; }, // 观察异常，避免 UnobservedTaskException
                    TaskContinuationOptions.OnlyOnFaulted);
        };

        engine.TagPolled += handler;
        return new Subscription(() => engine.TagPolled -= handler);
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _unsubscribe;

        public Subscription(Action unsubscribe) => _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
