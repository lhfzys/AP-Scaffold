using AP.Contracts.Hardware.Services;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Contracts.Security.Models;
using AP.Infra.Hardware.Services;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace AP.Infra.Tests.Hardware;

public class AuditingPlcServiceDecoratorTests
{
    private readonly IPlcService _inner = Substitute.For<IPlcService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly IIdentityService _identityService = Substitute.For<IIdentityService>();

    public AuditingPlcServiceDecoratorTests()
    {
        _identityService.CurrentUser.Returns(new UserInfo { UserName = "admin" });
    }

    private AuditingPlcServiceDecorator CreateDecorator()
        => new(_inner, _auditService, _identityService);

    [Fact]
    public async Task WriteAsync_Success_LogsAudit()
    {
        var sut = CreateDecorator();

        await sut.WriteAsync("D100", 123);

        await _auditService.Received(1).LogAsync(Arg.Is<AuditLogEntry>(e =>
            e.UserName == "admin" &&
            e.ActionType == AuditActionType.ManualControl &&
            e.TargetId == "D100" &&
            e.Description!.Contains("123") &&
            e.Succeeded));
    }

    [Fact]
    public async Task WriteAsync_Failure_LogsFailedAuditAndRethrows()
    {
        _inner.WriteAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("PLC 无响应"));
        var sut = CreateDecorator();

        var act = () => sut.WriteAsync("D100", 123);

        await act.Should().ThrowAsync<InvalidOperationException>();
        await _auditService.Received(1).LogAsync(Arg.Is<AuditLogEntry>(e =>
            !e.Succeeded && e.ErrorMessage == "PLC 无响应"));
    }

    [Fact]
    public async Task WriteAsync_NoAuditService_StillWrites()
    {
        var sut = new AuditingPlcServiceDecorator(_inner);

        await sut.WriteAsync("D100", 1);

        await _inner.Received(1).WriteAsync("D100", 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_NoCurrentUser_LogsSystem()
    {
        _identityService.CurrentUser.Returns((UserInfo?)null);
        var sut = CreateDecorator();

        await sut.WriteAsync("D100", 1);

        await _auditService.Received(1).LogAsync(Arg.Is<AuditLogEntry>(e => e.UserName == "system"));
    }

    [Fact]
    public async Task WriteBatchAsync_LogsAllAddresses()
    {
        var batchInner = Substitute.For<IPlcService, IPlcBatchReadWrite>();
        var sut = new AuditingPlcServiceDecorator(batchInner, _auditService, _identityService);
        var data = new Dictionary<string, object> { ["D100"] = 1, ["D101"] = 2 };

        await sut.WriteBatchAsync(data);

        await _auditService.Received(1).LogAsync(Arg.Is<AuditLogEntry>(e =>
            e.ActionName == "PLC 批量写入" &&
            e.TargetId!.Contains("D100") &&
            e.TargetId.Contains("D101") &&
            e.Succeeded));
    }

    [Fact]
    public async Task ReadAsync_DoesNotLogAudit()
    {
        _inner.ReadAsync<int>(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(42);
        var sut = CreateDecorator();

        var value = await sut.ReadAsync<int>("D100");

        value.Should().Be(42);
        await _auditService.DidNotReceive().LogAsync(Arg.Any<AuditLogEntry>());
    }
}
