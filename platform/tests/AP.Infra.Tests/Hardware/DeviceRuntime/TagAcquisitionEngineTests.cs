using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class TagAcquisitionEngineTests
{
    [Fact]
    public async Task Start_PollsAndWritesLatestValueStore()
    {
        var (engine, store, _) = Create([Tag("T1")], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1")?.Version >= 2, "应持续轮询并递增版本");

        store.Get("T1")!.Value.Should().Be((short)42);
        engine.Dispose();
    }

    [Fact]
    public async Task WriteOnlyTags_AreSkipped()
    {
        var (engine, store, tagService) = Create(
            [Tag("T1"), Tag("T2", TagAccess.WriteOnly)], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "T1 应被采集");
        await Task.Delay(150);

        store.Get("T2").Should().BeNull();
        tagService.ReadNames.Should().NotContain("T2");
        engine.Dispose();
    }

    [Fact]
    public async Task TagPolled_FiresWithChangedFlag()
    {
        var (engine, _, _) = Create([Tag("T1")], intervalMs: 20);
        var events = new List<TagPolledEventArgs>();
        engine.TagPolled += (_, args) => events.Add(args);

        engine.Start();
        await WaitUntilAsync(() => events.Count >= 2, "应触发多次采集事件");

        events[0].Changed.Should().BeTrue();          // 首次写入为变化
        events[0].Name.Should().Be("T1");
        events.Skip(1).Should().OnlyContain(e => !e.Changed); // 值未变不再标记
        engine.Dispose();
    }

    [Fact]
    public async Task BadValues_AreWrittenToStore()
    {
        var (engine, store, tagService) = Create([Tag("T1")], intervalMs: 20);
        tagService.Responder = _ => TagValue.Bad("设备未连接");

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "Bad 值也应写入最新值表");

        store.Get("T1")!.Quality.Should().Be(TagQuality.Bad);
        store.Get("T1")!.Error.Should().Be("设备未连接");
        engine.Dispose();
    }

    [Fact]
    public async Task Stop_StopsPolling()
    {
        var (engine, _, tagService) = Create([Tag("T1")], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => tagService.ReadNames.Count >= 1, "应已开始采集");
        engine.Stop();

        var countAfterStop = tagService.ReadNames.Count;
        await Task.Delay(150);

        tagService.ReadNames.Count.Should().Be(countAfterStop);
        engine.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task AcquisitionConfig_OverrideAndDefault_AreRespected()
    {
        var config = new TagAcquisitionConfig
        {
            DefaultIntervalMs = 1000,
            Overrides = { ["A.B"] = 250 }
        };

        config.GetIntervalMs("A.B").Should().Be(250);
        config.GetIntervalMs("C.D").Should().Be(1000);
        await Task.CompletedTask;
    }

    // --- 测试基础设施 ---

    private static (TagAcquisitionEngine Engine, LatestTagValueStore Store, FakeTagService TagService) Create(
        ResolvedTag[] tags, int intervalMs)
    {
        var table = new FakeTagTable(tags);
        var tagService = new FakeTagService();
        var store = new LatestTagValueStore();
        var engine = new TagAcquisitionEngine(
            table,
            new TagAcquisitionConfig { DefaultIntervalMs = intervalMs },
            tagService,
            store,
            Substitute.For<ILogger<TagAcquisitionEngine>>());
        return (engine, store, tagService);
    }

    private static ResolvedTag Tag(string name, TagAccess access = TagAccess.ReadWrite)
    {
        return new ResolvedTag(
            new TagDefinition { Name = name, DeviceId = "plc.main", Address = "D0", DataType = TagDataType.Int16, Access = access },
            new FakeParsedAddress("D0"));
    }

    private sealed record FakeParsedAddress(string Address)
    {
        public override string ToString() => Address;
    }

    private sealed class FakeTagTable(ResolvedTag[] tags) : ITagTable
    {
        public IReadOnlyCollection<ResolvedTag> Tags => tags;
        public ResolvedTag? Find(string name) =>
            tags.FirstOrDefault(t => string.Equals(t.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FakeTagService : ITagService
    {
        public List<string> ReadNames { get; } = [];
        public Func<string, TagValue> Responder = _ => TagValue.Good((short)42);

        public Task<TagValue> ReadAsync(string name, CancellationToken ct = default)
        {
            lock (ReadNames) ReadNames.Add(name);
            return Task.FromResult(Responder(name));
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
