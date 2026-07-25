using AP.Contracts.Hardware.Models;
using FluentAssertions;
using Xunit;

namespace AP.Infra.Tests.Hardware.DeviceRuntime;

/// <summary>
/// T1.7 看门狗/重连参数配置化：新增配置键的缺省值必须与既有硬编码参数一致（行为不变承诺）。
/// </summary>
public class PlcSupervisorOptionsDefaultsTests
{
    [Fact]
    public void PlcOptions_SupervisorDefaults_MatchLegacyHardcodedValues()
    {
        var options = new PlcOptions();

        options.HeartbeatIntervalSeconds.Should().Be(2);
        options.ReconnectBackoffSeconds.Should().Be(5);
        options.SupervisorRestartDelaySeconds.Should().Be(5);
    }

    [Fact]
    public void PlcOptions_SupervisorValues_CanBeCustomized()
    {
        var options = new PlcOptions
        {
            HeartbeatIntervalSeconds = 1,
            ReconnectBackoffSeconds = 10,
            SupervisorRestartDelaySeconds = 30
        };

        options.HeartbeatIntervalSeconds.Should().Be(1);
        options.ReconnectBackoffSeconds.Should().Be(10);
        options.SupervisorRestartDelaySeconds.Should().Be(30);
    }
}
