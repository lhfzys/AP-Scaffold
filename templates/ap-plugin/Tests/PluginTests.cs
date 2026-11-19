using AP.Plugin.Sample.Configuration;
using AP.Plugin.Sample.Reporting;
using AP.Plugin.Sample.ViewModels;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AP.Plugin.Sample.Tests;

public class PluginTests
{
    private static IConfiguration BuildConfiguration(int step = 2) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{PluginOptions.SectionName}:Greeting"] = "测试欢迎语",
                [$"{PluginOptions.SectionName}:Step"] = step.ToString()
            })
            .Build();

    [Fact]
    public void ViewModel_LoadsOptions_AndIncrements()
    {
        var vm = new PluginViewModel(BuildConfiguration(step: 3));

        vm.Message.Should().Be("测试欢迎语");
        vm.IncrementCommand.Execute(null);
        vm.Count.Should().Be(3);
        vm.ResetCommand.Execute(null);
        vm.Count.Should().Be(0);
    }

    [Fact]
    public void SettingsEditor_RoundTripsOptions()
    {
        var editor = new PluginSettingsViewModel();
        editor.LoadFromConfiguration(BuildConfiguration(step: 5));

        editor.Step.Should().Be(5);
        editor.Validate().Should().BeEmpty();
        var value = editor.GetConfigurationValue().Should().BeOfType<PluginOptions>().Subject;
        value.Step.Should().Be(5);
    }

    [Fact]
    public async Task ReportProvider_ReturnsData()
    {
        var data = await new PluginReportProvider().GetReportDataAsync(DateTime.Today);
        data.Rows.Should().NotBeEmpty();
        data.Headers.Should().Contain("项目");
    }
}
