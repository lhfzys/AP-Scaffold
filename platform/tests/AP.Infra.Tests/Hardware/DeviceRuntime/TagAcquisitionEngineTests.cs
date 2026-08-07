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
        var (engine, store, fakes) = Create(
            [Tag("T1"), Tag("T2", TagAccess.WriteOnly)], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "T1 应被采集");
        await Task.Delay(150);

        store.Get("T2").Should().BeNull();
        fakes.TagService.ReadNames.Should().NotContain("T2");
        engine.Dispose();
    }

    [Fact]
    public async Task TagPolled_FiresWithChangedFlag()
    {
        var (engine, store, _) = Create([Tag("T1")], intervalMs: 20);
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
        var (engine, store, fakes) = Create([Tag("T1")], intervalMs: 20);
        fakes.TagService.Responder = _ => TagValue.Bad("设备未连接");

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "Bad 值也应写入最新值表");

        store.Get("T1")!.Quality.Should().Be(TagQuality.Bad);
        store.Get("T1")!.Error.Should().Be("设备未连接");
        engine.Dispose();
    }

    [Fact]
    public async Task Stop_StopsPolling()
    {
        var (engine, _, fakes) = Create([Tag("T1")], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => fakes.TagService.ReadNames.Count >= 1, "应已开始采集");
        engine.Stop();

        var countAfterStop = fakes.TagService.ReadNames.Count;
        await Task.Delay(150);

        fakes.TagService.ReadNames.Count.Should().Be(countAfterStop);
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

    // --- TP3：批量合并读 ---

    [Fact]
    public async Task ConnectedPlc_UsesTypedBatchRead_OncePerCycle()
    {
        var (engine, store, fakes) = Create([Tag("T1"), Tag("T2")], intervalMs: 20, plcConnected: true);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1")?.Version >= 2, "应批量采集");

        fakes.TypedBatch.CallCount.Should().BeGreaterThan(0);
        fakes.TagService.ReadNames.Should().BeEmpty(); // 在线 PLC 不走逐点
        store.Get("T1")!.Value.Should().Be((short)42);
        engine.Dispose();
    }

    [Fact]
    public async Task BatchResultMissingAddress_WritesBad()
    {
        var (engine, store, fakes) = Create([Tag("T1"), Tag("T2")], intervalMs: 20, plcConnected: true);
        // 批量成功但缺 T2(D1) 的结果
        fakes.TypedBatch.Responder = items =>
        {
            var dict = new Dictionary<string, object> { ["D0"] = (short)42 }; // 只有 D0(T1)，缺 D1(T2)
            return Task.FromResult(dict);
        };

        engine.Start();
        await WaitUntilAsync(() => store.Get("T2") != null, "T2 应被写入");

        store.Get("T1")!.Quality.Should().Be(TagQuality.Good);
        store.Get("T2")!.Quality.Should().Be(TagQuality.Bad);
        store.Get("T2")!.Error.Should().Contain("缺少");
        engine.Dispose();
    }

    [Fact]
    public async Task BatchFailure_FallsBackToIndividualThisCycle()
    {
        var (engine, store, fakes) = Create([Tag("T1")], intervalMs: 20, plcConnected: true);
        fakes.TypedBatch.Responder = _ => throw new Exception("批量失败");

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "降级后逐点应采集到值");

        fakes.TypedBatch.CallCount.Should().BeGreaterThan(0);      // 尝试过批量
        fakes.TagService.ReadNames.Should().Contain("T1");          // 本轮降级逐点
        store.Get("T1")!.Value.Should().Be((short)42);
        engine.Dispose();
    }

    [Fact]
    public async Task NotSupportedBatch_PermanentlyFallsBack()
    {
        var (engine, store, fakes) = Create([Tag("T1")], intervalMs: 20, plcConnected: true);
        fakes.TypedBatch.Responder = _ => throw new NotSupportedException("不支持批量");

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1")?.Version >= 3, "应持续逐点采集");
        await Task.Delay(100);

        fakes.TypedBatch.CallCount.Should().Be(1); // 只尝试一次批量便永久降级
        engine.Dispose();
    }

    [Fact]
    public async Task DisconnectedDevice_SkipsBatch_GoesIndividual()
    {
        var (engine, store, fakes) = Create([Tag("T1")], intervalMs: 20, plcConnected: false);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "应逐点采集");

        fakes.TypedBatch.CallCount.Should().Be(0); // 未连接不尝试批量
        fakes.TagService.ReadNames.Should().Contain("T1");
        engine.Dispose();
    }

    [Fact]
    public async Task NonPlcDevice_GoesIndividual()
    {
        var (engine, store, fakes) = Create([Tag("T1", deviceId: "scanner.com3")], intervalMs: 20,
            plcConnected: true, deviceType: DeviceType.Scanner);

        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "应逐点采集");

        fakes.TypedBatch.CallCount.Should().Be(0);
        engine.Dispose();
    }

    // --- 热重载：Restart 重建分组 ---

    [Fact]
    public async Task Restart_RebuildsGroupsFromLatestTable()
    {
        var (engine, store, fakes) = Create([Tag("T1")], intervalMs: 20);
        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "T1 应被采集");

        // 热重载后点表换成 T2（T1 删除）；Restart 应按新表重建分组
        fakes.Table.Tags = [Tag("T2")];
        engine.Restart();
        await WaitUntilAsync(() => store.Get("T2") != null, "Restart 后应采集新表 T2");

        engine.IsRunning.Should().BeTrue();
        engine.Dispose();
    }

    // --- 读次统计（ITagAcquisitionStatus.TotalReads / FailedReads） ---

    [Fact]
    public async Task AllGoodReads_CountTotalOnly()
    {
        var (engine, _, _) = Create([Tag("T1"), Tag("T2")], intervalMs: 20);

        engine.Start();
        await WaitUntilAsync(() => engine.TotalReads >= 6, "应持续累计读次");

        engine.FailedReads.Should().Be(0);
        engine.Dispose();
    }

    [Fact]
    public async Task BadReads_CountAsFailed()
    {
        var (engine, _, fakes) = Create([Tag("T1")], intervalMs: 20);
        fakes.TagService.Responder = _ => TagValue.Bad("设备未连接");

        engine.Start();
        await WaitUntilAsync(() => engine.TotalReads >= 3, "应持续累计读次");

        engine.FailedReads.Should().Be(engine.TotalReads); // 全部 Bad
        engine.Dispose();
    }

    [Fact]
    public async Task BatchMissingAddress_CountsFailed()
    {
        var (engine, _, fakes) = Create([Tag("T1"), Tag("T2")], intervalMs: 20, plcConnected: true);
        fakes.TypedBatch.Responder = items =>
            Task.FromResult(new Dictionary<string, object> { ["D0"] = (short)42 }); // 缺 D1(T2)

        engine.Start();
        await WaitUntilAsync(() => engine.TotalReads >= 4, "应累计两轮以上读次");

        engine.FailedReads.Should().BeGreaterThan(0);
        engine.FailedReads.Should().BeLessThan(engine.TotalReads); // T1 Good / T2 Bad
        engine.Dispose();
    }

    // --- 测试基础设施 ---

    private static (TagAcquisitionEngine Engine, LatestTagValueStore Store, Fakes Fakes) Create(
        ResolvedTag[] tags, int intervalMs, bool plcConnected = false, DeviceType deviceType = DeviceType.Plc)
    {
        var fakes = new Fakes(deviceType, plcConnected);
        var store = new LatestTagValueStore();
        fakes.Table = new FakeTagTable(tags, new TagAcquisitionConfig { DefaultIntervalMs = intervalMs });
        var engine = new TagAcquisitionEngine(
            fakes.Table,
            fakes.TagService,
            fakes.TypedBatch,
            fakes.Registry,
            store,
            Substitute.For<ILogger<TagAcquisitionEngine>>());
        return (engine, store, fakes);
    }

    private static ResolvedTag Tag(string name, TagAccess access = TagAccess.ReadWrite, string deviceId = "plc.main")
    {
        var address = name == "T2" ? "D1" : "D0";
        return new ResolvedTag(
            new TagDefinition { Name = name, DeviceId = deviceId, Address = address, DataType = TagDataType.Int16, Access = access },
            new FakeParsedAddress(address));
    }

    private sealed record FakeParsedAddress(string Address)
    {
        public override string ToString() => Address;
    }

    private sealed class FakeTagTable(ResolvedTag[] tags, TagAcquisitionConfig acquisition) : ITagTable
    {
        public IReadOnlyCollection<ResolvedTag> Tags { get; set; } = tags;
        public TagAcquisitionConfig Acquisition { get; set; } = acquisition;

        public ResolvedTag? Find(string name) =>
            Tags.FirstOrDefault(t => string.Equals(t.Definition.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed class Fakes
    {
        public Fakes(DeviceType deviceType, bool connected)
        {
            TagService = new FakeTagService();
            TypedBatch = new FakeTypedBatchRead();

            var device = Substitute.For<IDevice>();
            device.Info.Returns(new DeviceInfo("plc.main", "主 PLC", deviceType, "Test"));
            device.State.Returns(connected ? DeviceConnectionState.Connected : DeviceConnectionState.Reconnecting);
            Registry = Substitute.For<IDeviceRegistry>();
            Registry.Find(Arg.Any<string>()).Returns(device);
        }

        public FakeTagService TagService { get; }
        public FakeTypedBatchRead TypedBatch { get; }
        public IDeviceRegistry Registry { get; }
        public FakeTagTable Table { get; set; } = null!;
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

    private sealed class FakeTypedBatchRead : IPlcTypedBatchRead
    {
        public int CallCount;
        public Func<IReadOnlyList<BatchReadItem>, Task<Dictionary<string, object>>> Responder = items =>
            Task.FromResult(items.ToDictionary(i => i.Address, i => (object)(short)42));

        public Task<Dictionary<string, object>> ReadBatchAsync(IReadOnlyList<BatchReadItem> items, CancellationToken ct = default)
        {
            Interlocked.Increment(ref CallCount);
            return Responder(items);
        }
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
