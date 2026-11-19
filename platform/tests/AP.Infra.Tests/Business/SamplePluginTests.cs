using AP.Plugin.Sample;
using AP.Plugin.Sample.Configuration;
using AP.Plugin.Sample.Reporting;
using AP.Plugin.Sample.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AP.Infra.Tests.Business;

public class SamplePluginTests
{
    private static IConfiguration BuildConfiguration(int step = 2) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{SampleOptions.SectionName}:Greeting"] = "欢迎使用示例",
                [$"{SampleOptions.SectionName}:Step"] = step.ToString()
            })
            .Build();

    [Fact]
    public void ViewModel_LoadsOptions_AndIncrementsByStep()
    {
        var vm = new SampleViewModel(BuildConfiguration(step: 3));

        vm.Message.Should().Be("欢迎使用示例");
        vm.IncrementCommand.Execute(null);
        vm.Count.Should().Be(3);
        vm.IncrementCommand.Execute(null);
        vm.Count.Should().Be(6);

        vm.ResetCommand.Execute(null);
        vm.Count.Should().Be(0);
    }

    [Fact]
    public void SettingsEditor_RoundTripsOptions()
    {
        var editor = new SampleSettingsViewModel();
        editor.LoadFromConfiguration(BuildConfiguration(step: 5));

        editor.Greeting.Should().Be("欢迎使用示例");
        editor.Step.Should().Be(5);
        editor.Validate().Should().BeEmpty();

        var value = editor.GetConfigurationValue().Should().BeOfType<SampleOptions>().Subject;
        value.Greeting.Should().Be("欢迎使用示例");
        value.Step.Should().Be(5);
    }

    [Fact]
    public async Task ReportProvider_ReturnsReportData()
    {
        var data = await new SampleReportDataProvider().GetReportDataAsync(DateTime.Today);

        data.ReportType.Should().Be("SampleBusiness");
        data.Headers.Should().Contain("项目");
        data.Rows.Should().NotBeEmpty();
    }

    [Fact]
    public void Plugin_ExposesNavigationEntry()
    {
        var plugin = new SamplePlugin(NullLogger.Instance);
        var menu = plugin.GetMenuItems().Single();

        menu.NavigationTarget.Should().Be("SampleView");
        menu.Label.Should().Be("示例业务");
    }
}
