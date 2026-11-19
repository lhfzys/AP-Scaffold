using AP.Plugin.SystemSettings.Editors;
using AP.Shared.Utilities.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AP.Infra.Tests.Configuration;

public class AppConfigurationEditorTests
{
    [Fact]
    public void LoadAndSave_PreservesLayoutAndNavigationSettings_WhenEditingBasicFields()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppConfiguration:CompanyName"] = "旧公司",
                ["AppConfiguration:SoftwareName"] = "旧软件",
                ["AppConfiguration:MachineId"] = "M-01",
                ["AppConfiguration:MachineName"] = "旧工位",
                ["AppConfiguration:LayoutMode"] = "SinglePage",
                ["AppConfiguration:DefaultNavigationTarget"] = "TagTableListView",
                ["AppConfiguration:NavigationWhenSecurityDisabled:0"] = "DashboardView",
                ["AppConfiguration:NavigationWhenSecurityDisabled:1"] = "SettingsShellView"
            })
            .Build();

        var editor = new AppConfigurationEditorViewModel();
        editor.LoadFromConfiguration(configuration);

        editor.CompanyName = "新公司";
        var value = editor.GetConfigurationValue().Should().BeOfType<AppConfigurationOptions>().Subject;

        value.CompanyName.Should().Be("新公司");
        value.LayoutMode.Should().Be("SinglePage");
        value.DefaultNavigationTarget.Should().Be("TagTableListView");
        value.NavigationWhenSecurityDisabled.Should().Equal("DashboardView", "SettingsShellView");
    }
}
