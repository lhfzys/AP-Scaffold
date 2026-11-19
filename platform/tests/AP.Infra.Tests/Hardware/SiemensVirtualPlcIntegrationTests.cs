using AP.Contracts.Hardware.Models;
using AP.Contracts.Hardware.Services;
using AP.Infra.Hardware.Services;
using AP.Plugin.Plc.Siemens.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Polly;
using Xunit;

namespace AP.Infra.Tests.Hardware;

/// <summary>
/// 人工触发的西门子虚拟 PLC 冒烟测试。
/// 默认跳过；启动虚拟 S7 服务器后设置 AP_PLC_TEST_SERVER=1 再运行。
/// 默认目标 127.0.0.1:102 / DB1.0.0；可用 AP_PLC_TEST_HOST/PORT/HEARTBEAT 覆盖。
/// </summary>
public class SiemensVirtualPlcIntegrationTests
{
    [Fact]
    public async Task TestConnection_AgainstVirtualSiemens()
    {
        if (Environment.GetEnvironmentVariable("AP_PLC_TEST_SERVER") != "1")
            return;

        var options = new PlcOptions
        {
            DriverType = "Siemens",
            IpAddress = Environment.GetEnvironmentVariable("AP_PLC_TEST_HOST") ?? "127.0.0.1",
            Port = int.TryParse(Environment.GetEnvironmentVariable("AP_PLC_TEST_PORT"), out var port) ? port : 102,
            Model = "S7_1200",
            HeartbeatAddress = Environment.GetEnvironmentVariable("AP_PLC_TEST_HEARTBEAT") ?? "DB1.0.0",
            Timeout = 3000
        };

        var driver = new SiemensPlcService(
            Options.Create(options),
            ResiliencePipeline.Empty,
            NullLogger<SiemensPlcService>.Instance,
            Substitute.For<IMediator>());

        var factory = Substitute.For<IPlcDriverFactory>();
        factory.DriverType.Returns("Siemens");
        factory.CreateDriver(Arg.Any<PlcOptions>(), Arg.Any<IServiceProvider>()).Returns(driver);

        var registry = new PlcDriverRegistry();
        registry.Register(factory);
        var tester = new PlcConnectionTester(
            registry, Substitute.For<IServiceProvider>(), NullLogger<PlcConnectionTester>.Instance);

        var result = await tester.TestAsync(options);

        result.Connected.Should().BeTrue(result.Message);
        result.ReadOk.Should().BeTrue(result.Message);
        System.Diagnostics.Debug.WriteLine(result.Message);
    }
}
