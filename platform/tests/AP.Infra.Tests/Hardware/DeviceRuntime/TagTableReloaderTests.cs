using System.Diagnostics;
using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

/// <summary>点表热重载编排：换表 + 引擎重启 + 值表清理 的端到端验证。</summary>
public class TagTableReloaderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public async Task Reload_SwapsTable_RestartsEngine_PrunesStore()
    {
        var path = WriteTags("""
            { "Acquisition": { "DefaultIntervalMs": 20 },
              "Tags": [ { "Name": "T1", "DeviceId": "plc.main", "Address": "raw1", "DataType": "Int16" } ] }
            """);
        var registry = new DeviceRegistry();
        registry.Register(FakeDevice());
        var table = new TagTable(registry, [new FakeValidator()], path);
        var store = new LatestTagValueStore();
        var engine = new TagAcquisitionEngine(
            table,
            new FakeTagService(),
            Substitute.For<IPlcTypedBatchRead>(),
            registry,
            store,
            Substitute.For<ILogger<TagAcquisitionEngine>>());
        engine.Start();
        await WaitUntilAsync(() => store.Get("T1") != null, "T1 应被采集");

        // 编辑保存后：点表只剩 T2（T1 删除）
        File.WriteAllText(path, """
            { "Acquisition": { "DefaultIntervalMs": 20 },
              "Tags": [ { "Name": "T2", "DeviceId": "plc.main", "Address": "raw2", "DataType": "Int16" } ] }
            """);

        var reloader = new TagTableReloader(table, engine, store, Substitute.For<ILogger<TagTableReloader>>());
        var result = reloader.Reload();

        result.Success.Should().BeTrue();
        store.Get("T1").Should().BeNull(); // 已删除点的残留值被清理
        await WaitUntilAsync(() => store.Get("T2") != null, "新表 T2 应被采集");
        engine.IsRunning.Should().BeTrue();
        engine.Dispose();
    }

    [Fact]
    public void Reload_InvalidContent_KeepsOldTableAndEngineUntouched()
    {
        var path = WriteTags("""
            { "Tags": [ { "Name": "T1", "DeviceId": "plc.main", "Address": "raw1", "DataType": "Int16" } ] }
            """);
        var registry = new DeviceRegistry();
        registry.Register(FakeDevice());
        var table = new TagTable(registry, [new FakeValidator()], path);
        var engine = new TagAcquisitionEngine(
            table,
            new FakeTagService(),
            Substitute.For<IPlcTypedBatchRead>(),
            registry,
            new LatestTagValueStore(),
            Substitute.For<ILogger<TagAcquisitionEngine>>());

        File.WriteAllText(path, """
            { "Tags": [ { "Name": "T2", "DeviceId": "plc.main", "Address": "BAD!", "DataType": "Int16" } ] }
            """);

        var reloader = new TagTableReloader(table, engine, new LatestTagValueStore(), Substitute.For<ILogger<TagTableReloader>>());
        var result = reloader.Reload();

        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        table.Find("T1").Should().NotBeNull(); // 旧表保留
        engine.IsRunning.Should().BeFalse();   // 未运行的引擎不被误启动
        engine.Dispose();
    }

    public void Dispose()
    {
        foreach (var file in _tempFiles)
            if (File.Exists(file)) File.Delete(file);
    }

    private string WriteTags(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        _tempFiles.Add(path);
        return path;
    }

    private static IDevice FakeDevice()
    {
        var device = Substitute.For<IDevice>();
        device.Info.Returns(new DeviceInfo("plc.main", "plc.main", DeviceType.Plc, "Test"));
        return device;
    }

    /// <summary>假验证器：仅拒绝含 '!' 的地址，其余原样包装。</summary>
    private sealed class FakeValidator : IAddressValidator
    {
        public string DriverType => "Test";

        public bool TryParse(string address, out object? parsedAddress, out string? error)
        {
            if (address.Contains('!'))
            {
                parsedAddress = null;
                error = "地址非法";
                return false;
            }

            parsedAddress = address;
            error = null;
            return true;
        }
    }

    private sealed class FakeTagService : ITagService
    {
        public Task<TagValue> ReadAsync(string name, CancellationToken ct = default) =>
            Task.FromResult(TagValue.Good((short)42));

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
