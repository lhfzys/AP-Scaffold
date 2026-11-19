using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Plugin.SystemSettings.Editors;
using CommunityToolkit.Mvvm.Input;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Configuration;

public class PlcConfigurationEditorViewModelTests
{
    [Fact]
    public async Task TestConnectionCommand_ShowsSuccessMessage()
    {
        var tester = Substitute.For<IPlcConnectionTester>();
        tester.TestAsync(Arg.Any<PlcOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new PlcConnectionTestResult(
                true, true, "连接成功，读取 DB1.0.0 = True", 42, "True")));

        var vm = new PlcConfigurationEditorViewModel(tester);

        await ((IAsyncRelayCommand)vm.TestConnectionCommand).ExecuteAsync(null);

        vm.TestSucceeded.Should().BeTrue();
        vm.TestMessage.Should().Contain("连接成功");
        await tester.Received(1).TestAsync(Arg.Any<PlcOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestConnectionCommand_InvalidConfiguration_DoesNotCallTester()
    {
        var tester = Substitute.For<IPlcConnectionTester>();
        var vm = new PlcConfigurationEditorViewModel(tester);
        vm.IpAddress = "not-an-ip";

        await ((IAsyncRelayCommand)vm.TestConnectionCommand).ExecuteAsync(null);

        vm.TestSucceeded.Should().BeFalse();
        vm.TestMessage.Should().Contain("IP 地址格式不正确");
        await tester.DidNotReceive().TestAsync(Arg.Any<PlcOptions>(), Arg.Any<CancellationToken>());
    }
}
