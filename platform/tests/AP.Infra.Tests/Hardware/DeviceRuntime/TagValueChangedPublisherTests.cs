using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TagValueChangedPublisherTests
{
    [Fact]
    public async Task ChangedValue_PublishesEvent()
    {
        var (engine, tagService) = CreateEngine();
        var (mediator, published) = CreateMediator();
        using var _ = new TagValueChangedPublisher(mediator).Attach(engine);

        engine.Start();
        await WaitUntilAsync(() => published.Count >= 1, "变化应发布事件");

        published[0].Name.Should().Be("T1");
        published[0].Value.Quality.Should().Be(TagQuality.Good);
        engine.Dispose();
    }

    [Fact]
    public async Task UnchangedValue_PublishesOnlyOnce()
    {
        var (engine, tagService) = CreateEngine();
        var (mediator, published) = CreateMediator();
        using var _ = new TagValueChangedPublisher(mediator).Attach(engine);

        engine.Start();
        await WaitUntilAsync(() => tagService.ReadCount >= 5, "应多轮采集");
        await Task.Delay(100);

        published.Should().HaveCount(1); // 值恒定：仅首次变化发布
        engine.Dispose();
    }

    [Fact]
    public async Task Dispose_Unsubscribes_NoFurtherEvents()
    {
        var (engine, tagService) = CreateEngine();
        var (mediator, published) = CreateMediator();
        var subscription = new TagValueChangedPublisher(mediator).Attach(engine);
        subscription.Dispose();

        engine.Start();
        await WaitUntilAsync(() => tagService.ReadCount >= 1, "应已采集");
        await Task.Delay(100);

        published.Should().BeEmpty();
        engine.Dispose();
    }

    private static (IMediator Mediator, List<TagValueChangedEvent> Published) CreateMediator()
    {
        var published = new List<TagValueChangedEvent>();
        var mediator = Substitute.For<IMediator>();
        mediator.Publish(Arg.Do<TagValueChangedEvent>(e => { lock (published) published.Add(e); }), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        return (mediator, published);
    }

    private static (TagAcquisitionEngine Engine, FakeTagService TagService) CreateEngine()
    {
        var tag = new ResolvedTag(
            new TagDefinition { Name = "T1", DeviceId = "plc.main", Address = "D0", DataType = TagDataType.Int16 },
            new object());
        var tagService = new FakeTagService();
        var registry = Substitute.For<IDeviceRegistry>();
        registry.Find(Arg.Any<string>()).Returns((IDevice?)null); // 无设备 → 逐点路径
        var engine = new TagAcquisitionEngine(
            new FakeTagTable(tag, new TagAcquisitionConfig { DefaultIntervalMs = 20 }),
            tagService,
            Substitute.For<IPlcTypedBatchRead>(),
            registry,
            new LatestTagValueStore(),
            Substitute.For<ILogger<TagAcquisitionEngine>>());
        return (engine, tagService);
    }

    private sealed class FakeTagTable(ResolvedTag tag, TagAcquisitionConfig acquisition) : ITagTable
    {
        public IReadOnlyCollection<ResolvedTag> Tags => [tag];
        public TagAcquisitionConfig Acquisition { get; } = acquisition;
        public ResolvedTag? Find(string name) => tag;
    }

    private sealed class FakeTagService : ITagService
    {
        private int _readCount;

        public int ReadCount => _readCount;

        public Task<TagValue> ReadAsync(string name, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _readCount);
            return Task.FromResult(TagValue.Good((short)42));
        }

        public Task<TagValue> WriteAsync(string name, object? value, CancellationToken ct = default) =>
            Task.FromResult(TagValue.Good(value));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string because, int timeoutMs = 5000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition())
        {
            if (sw.ElapsedMilliseconds > timeoutMs)
                throw new Xunit.Sdk.XunitException($"等待超时: {because}");
            await Task.Delay(10);
        }
    }
}
