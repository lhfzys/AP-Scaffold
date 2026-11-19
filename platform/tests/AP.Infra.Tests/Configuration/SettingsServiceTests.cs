using System.Text.Json.Nodes;
using AP.Plugin.SystemSettings.Services;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.Utilities.Configuration;
using AP.Shared.Utilities.Paths;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Configuration;

[Collection("AppPathsStatic")]
public class SettingsServiceTests : IDisposable
{
    private readonly string _configDirectory = AppPaths.ConfigDirectory;
    private readonly string _configFile;
    private readonly List<string> _createdFiles = new();

    public SettingsServiceTests()
    {
        Directory.CreateDirectory(_configDirectory);
        _configFile = Path.Combine(_configDirectory, "appsettings.json");
        if (File.Exists(_configFile))
        {
            var existingBackup = _configFile + ".test-backup";
            File.Copy(_configFile, existingBackup, true);
            _createdFiles.Add(existingBackup);
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_MergesSection_AndBackupRestoreRoundTrips()
    {
        var original = """
            {
              "AppConfiguration": {
                "CompanyName": "旧公司",
                "LayoutMode": "SinglePage",
                "DefaultNavigationTarget": "TagTableListView",
                "UnknownKey": "keep-me"
              },
              "OtherSection": { "Value": 1 }
            }
            """;
        File.WriteAllText(_configFile, original);
        _createdFiles.Add(_configFile);

        var contributor = Substitute.For<ISettingsContributor>();
        contributor.ConfigurationSection.Returns("AppConfiguration");
        contributor.Title.Returns("应用基础信息");

        var editor = Substitute.For<ISettingsEditorViewModel>();
        editor.Validate().Returns(Array.Empty<string>());
        editor.RequiresRestart.Returns(false);
        editor.GetConfigurationValue().Returns(new AppConfigurationOptions
        {
            CompanyName = "新公司",
            SoftwareName = "新软件",
            MachineId = "M-01",
            MachineName = "本机",
            LayoutMode = "SinglePage",
            DefaultNavigationTarget = "TagTableListView",
            NavigationWhenSecurityDisabled = new List<string> { "DashboardView" }
        });

        var service = new SettingsService(
            new ConfigurationBuilder().Build(),
            NullLogger<SettingsService>.Instance);

        var backupsBefore = service.GetBackupFiles().ToHashSet(StringComparer.OrdinalIgnoreCase);

        var result = await service.SaveSettingsAsync(new[] { (contributor, editor) });
        result.Success.Should().BeTrue();

        var saved = JsonNode.Parse(File.ReadAllText(_configFile))!.AsObject();
        saved["AppConfiguration"]!["CompanyName"]!.GetValue<string>().Should().Be("新公司");
        saved["AppConfiguration"]!["UnknownKey"]!.GetValue<string>().Should().Be("keep-me");
        saved["OtherSection"]!["Value"]!.GetValue<int>().Should().Be(1);

        var backupPath = service.GetBackupFiles()
            .First(path => !backupsBefore.Contains(path));
        _createdFiles.Add(backupPath);

        service.RestoreFromBackup(backupPath).Should().BeTrue();
        var restored = JsonNode.Parse(File.ReadAllText(_configFile))!.AsObject();
        restored["AppConfiguration"]!["CompanyName"]!.GetValue<string>().Should().Be("旧公司");
    }

    public void Dispose()
    {
        foreach (var file in _createdFiles)
        {
            try { if (File.Exists(file)) File.Delete(file); } catch { }
        }
    }
}
