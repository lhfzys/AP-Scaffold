using AP.Contracts.Hardware.DeviceRuntime;
using AP.Infra.Hardware.DeviceRuntime;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

public class LatestTagValueStoreTests
{
    [Fact]
    public void FirstUpdate_Version1_AndChanged()
    {
        var store = new LatestTagValueStore();

        var (value, changed) = store.Update("A.B", (short)1, TagQuality.Good);

        value.Version.Should().Be(1);
        changed.Should().BeTrue();
    }

    [Fact]
    public void SameValueAgain_VersionIncrements_ButNotChanged()
    {
        var store = new LatestTagValueStore();
        store.Update("A.B", (short)1, TagQuality.Good);

        var (value, changed) = store.Update("A.B", (short)1, TagQuality.Good);

        value.Version.Should().Be(2);
        changed.Should().BeFalse();
    }

    [Fact]
    public void ChangedValue_IsDetected()
    {
        var store = new LatestTagValueStore();
        store.Update("A.B", (short)1, TagQuality.Good);

        var (_, changed) = store.Update("A.B", (short)2, TagQuality.Good);

        changed.Should().BeTrue();
    }

    [Fact]
    public void QualityChange_SameValue_IsDetected()
    {
        var store = new LatestTagValueStore();
        store.Update("A.B", null, TagQuality.Good);

        var (_, changed) = store.Update("A.B", null, TagQuality.Bad, "设备未连接");

        changed.Should().BeTrue();
    }

    [Fact]
    public void Get_CaseInsensitive_AndUnknownReturnsNull()
    {
        var store = new LatestTagValueStore();
        store.Update("A.B", (short)1, TagQuality.Good);

        store.Get("a.b").Should().NotBeNull();
        store.Get("ghost").Should().BeNull();
    }

    [Fact]
    public void Snapshot_ContainsAllTags()
    {
        var store = new LatestTagValueStore();
        store.Update("A", 1, TagQuality.Good);
        store.Update("B", 2, TagQuality.Good);

        store.Snapshot().Should().HaveCount(2);
    }

    [Fact]
    public void PruneExcept_RemovesStaleKeys_KeepsListed()
    {
        var store = new LatestTagValueStore();
        store.Update("A", 1, TagQuality.Good);
        store.Update("B", 2, TagQuality.Good);

        store.PruneExcept(["a"]); // 大小写不敏感

        store.Get("A").Should().NotBeNull();
        store.Get("B").Should().BeNull();
        store.Snapshot().Should().HaveCount(1);
    }
}
