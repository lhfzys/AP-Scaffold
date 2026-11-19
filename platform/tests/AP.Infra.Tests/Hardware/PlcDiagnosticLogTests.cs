using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class PlcDiagnosticLogTests
{
    [Fact]
    public void Record_GetRecent_ReturnsNewestFirstAndHonorsCapacity()
    {
        var log = new PlcDiagnosticLog(NullLogger<PlcDiagnosticLog>.Instance, capacity: 2);
        log.Record(new PlcDiagnosticEntry(DateTime.Now, "Read", "A", "1", 1, true, null));
        log.Record(new PlcDiagnosticEntry(DateTime.Now, "Read", "B", "2", 2, true, null));
        log.Record(new PlcDiagnosticEntry(DateTime.Now, "Write", "C", "3", 3, false, "err"));

        var recent = log.GetRecent();

        recent.Should().HaveCount(2);
        recent[0].Address.Should().Be("C");
        recent[1].Address.Should().Be("B");
    }

    [Fact]
    public async Task AuditingDecorator_ReadAndWrite_RecordDiagnostics()
    {
        var inner = Substitute.For<IPlcService>();
        inner.ReadAsync<bool>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));
        inner.WriteAsync(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var diagnostics = new PlcDiagnosticLog(NullLogger<PlcDiagnosticLog>.Instance);
        var decorator = new AuditingPlcServiceDecorator(
            inner, diagnosticLog: diagnostics);

        await decorator.ReadAsync<bool>("DB1.0.0");
        await decorator.WriteAsync("DB1.0.1", true);

        var entries = diagnostics.GetRecent();
        entries.Should().Contain(e => e.Operation == "Read<Boolean>" && e.Success);
        entries.Should().Contain(e => e.Operation == "Write<Boolean>" && e.Success);
    }

    [Fact]
    public void Clear_RemovesEntries()
    {
        var log = new PlcDiagnosticLog(NullLogger<PlcDiagnosticLog>.Instance);
        log.Record(new PlcDiagnosticEntry(DateTime.Now, "Read", "A", "1", 1, true, null));

        log.Clear();

        log.GetRecent().Should().BeEmpty();
    }
}
