using System.IO;
using System.Text.Json;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.Utilities.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AP.Plugin.SystemSettings.Services;

/// <summary>
/// 配置中心服务
/// 负责配置的备份、保存、审计和变更通知。
/// </summary>
public class SettingsService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<SettingsService> _logger;
    private readonly string _configDirectory;
    private readonly string _appSettingsPath;

    public SettingsService(IConfiguration configuration, ILogger<SettingsService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _configDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Configuration");
        _appSettingsPath = Path.Combine(_configDirectory, "appsettings.json");
    }

    /// <summary>
    /// 保存所有配置贡献者的编辑值
    /// </summary>
    /// <param name="editors">配置编辑器集合</param>
    /// <returns>保存结果，包含是否需要重启及错误信息</returns>
    public SaveSettingsResult SaveSettings(IReadOnlyList<(ISettingsContributor Contributor, ISettingsEditorViewModel Editor)> editors)
    {
        // 1. 统一验证
        var validationErrors = new List<string>();
        foreach (var (contributor, editor) in editors)
        {
            var errors = editor.Validate().ToList();
            if (errors.Count > 0)
                validationErrors.Add($"[{contributor.Title}] {string.Join("; ", errors)}");
        }

        if (validationErrors.Count > 0)
        {
            return new SaveSettingsResult
            {
                Success = false,
                Errors = validationErrors
            };
        }

        // 2. 备份现有配置
        var backupPath = BackupAppSettings();

        // 3. 保存每个配置节
        var requiresRestart = false;
        var changedSections = new List<string>();

        try
        {
            foreach (var (contributor, editor) in editors)
            {
                var oldValue = _configuration[contributor.ConfigurationSection];
                var newValue = editor.GetConfigurationValue();

                ConfigurationHelper.UpdateAppSetting(contributor.ConfigurationSection, newValue, "appsettings.json");

                changedSections.Add(contributor.ConfigurationSection);
                if (editor.RequiresRestart)
                    requiresRestart = true;

                _logger.LogInformation("配置已更新: {Section}，需要重启: {RequiresRestart}",
                    contributor.ConfigurationSection, editor.RequiresRestart);
            }

            _logger.LogInformation("配置保存成功，备份文件: {BackupPath}", backupPath);

            return new SaveSettingsResult
            {
                Success = true,
                BackupPath = backupPath,
                ChangedSections = changedSections,
                RequiresRestart = requiresRestart
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "保存配置失败");
            return new SaveSettingsResult
            {
                Success = false,
                Errors = new List<string> { $"保存配置失败: {ex.Message}" }
            };
        }
    }

    /// <summary>
    /// 备份 appsettings.json
    /// </summary>
    private string BackupAppSettings()
    {
        if (!File.Exists(_appSettingsPath))
            return string.Empty;

        var backupFileName = $"appsettings.backup.{DateTime.Now:yyyyMMddHHmmss}.json";
        var backupPath = Path.Combine(_configDirectory, backupFileName);
        File.Copy(_appSettingsPath, backupPath, overwrite: true);
        return backupPath;
    }

    /// <summary>
    /// 获取最近的备份文件列表
    /// </summary>
    public IReadOnlyList<string> GetBackupFiles()
    {
        if (!Directory.Exists(_configDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_configDirectory, "appsettings.backup.*.json")
            .OrderByDescending(Path.GetFileName)
            .ToList();
    }

    /// <summary>
    /// 从备份恢复
    /// </summary>
    public bool RestoreFromBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath)) return false;
            File.Copy(backupPath, _appSettingsPath, overwrite: true);
            _logger.LogInformation("配置已从备份恢复: {BackupPath}", backupPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "从备份恢复配置失败");
            return false;
        }
    }
}

/// <summary>
/// 保存配置结果
/// </summary>
public class SaveSettingsResult
{
    public bool Success { get; set; }
    public IReadOnlyList<string> Errors { get; set; } = new List<string>();
    public string BackupPath { get; set; } = string.Empty;
    public IReadOnlyList<string> ChangedSections { get; set; } = new List<string>();
    public bool RequiresRestart { get; set; }
}
