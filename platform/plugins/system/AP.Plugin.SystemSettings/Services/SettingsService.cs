using System.IO;
using System.Text.Json;
using AP.Contracts.Security.Abstractions;
using AP.Contracts.Security.Audit;
using AP.Shared.PluginSDK.Configuration;
using AP.Shared.Utilities.Helpers;
using AP.Shared.Utilities.Paths;
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
    private readonly IAuditService? _auditService;
    private readonly IIdentityService? _identityService;
    private readonly string _configDirectory;

    public SettingsService(
        IConfiguration configuration,
        ILogger<SettingsService> logger,
        IAuditService? auditService = null,
        IIdentityService? identityService = null)
    {
        _configuration = configuration;
        _logger = logger;
        _auditService = auditService;
        _identityService = identityService;
        _configDirectory = AppPaths.ConfigDirectory;
    }

    /// <summary>
    /// 保存所有配置贡献者的编辑值
    /// </summary>
    /// <param name="editors">配置编辑器集合</param>
    /// <returns>保存结果，包含是否需要重启及错误信息</returns>
    public async Task<SaveSettingsResult> SaveSettingsAsync(IReadOnlyList<(ISettingsContributor Contributor, ISettingsEditorViewModel Editor)> editors)
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

        // 2. 逐节解析写回目标文件（角色文件含该节 → 角色文件，否则基文件），并备份各目标文件
        var roleFileName = _configuration["AppRuntime:RoleConfigFile"];
        var targetFiles = editors
            .Select(e => ConfigurationHelper.ResolveTargetFileName(e.Contributor.ConfigurationSection, roleFileName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var requiresRestart = false;
        var changedSections = new List<string>();
        var backupPath = string.Empty;

        try
        {
            // 备份纳入 try：备份失败也应通过统一的“保存失败”路径反馈，而不是绕过错误提示
            var backupPaths = BackupConfigFiles(targetFiles);
            backupPath = string.Join("; ", backupPaths);

            foreach (var (contributor, editor) in editors)
            {
                var newValue = editor.GetConfigurationValue();

                var fileName = ConfigurationHelper.ResolveTargetFileName(contributor.ConfigurationSection, roleFileName);
                ConfigurationHelper.UpdateAppSetting(contributor.ConfigurationSection, newValue, fileName);

                changedSections.Add(contributor.ConfigurationSection);
                if (editor.RequiresRestart)
                    requiresRestart = true;

                _logger.LogInformation("配置已更新: {Section}，需要重启: {RequiresRestart}",
                    contributor.ConfigurationSection, editor.RequiresRestart);
            }

            _logger.LogInformation("配置保存成功，备份文件: {BackupPath}", backupPath);

            await LogAuditSafeAsync(true, changedSections, requiresRestart, null);

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
            await LogAuditSafeAsync(false, changedSections, requiresRestart, ex.Message);
            return new SaveSettingsResult
            {
                Success = false,
                Errors = new List<string> { $"保存配置失败：{ex.Message}" }
            };
        }
    }

    /// <summary>
    /// 记录配置修改审计日志（审计服务不可用时静默跳过，审计失败不影响保存结果）
    /// </summary>
    private async Task LogAuditSafeAsync(bool succeeded, IReadOnlyList<string> changedSections, bool requiresRestart, string? error)
    {
        if (_auditService is null) return;

        try
        {
            var sections = string.Join(", ", changedSections);
            await _auditService.LogAsync(new AuditLogEntry
            {
                Timestamp = DateTime.Now,
                // 未登录时记为 system；Security 禁用时 CurrentUser 恒为 anonymous
                UserName = _identityService?.CurrentUser?.UserName ?? "system",
                ActionType = AuditActionType.Update,
                ActionName = "修改系统配置",
                TargetId = sections,
                Description = $"更新配置节：{sections}；需要重启：{requiresRestart}",
                Succeeded = succeeded,
                ErrorMessage = error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "记录配置修改审计日志失败");
        }
    }

    /// <summary>
    /// 备份各目标配置文件（不存在的文件跳过；备份名规则 {文件名去扩展名}.backup.{时间戳}.json）
    /// </summary>
    private IReadOnlyList<string> BackupConfigFiles(IReadOnlyList<string> fileNames)
    {
        var backups = new List<string>();
        foreach (var fileName in fileNames)
        {
            // 覆盖层尚未生成时，备份安装目录的默认文件，保证“保存前状态”可追溯
            var sourcePath = Path.Combine(_configDirectory, fileName);
            if (!File.Exists(sourcePath))
            {
                var defaultPath = Path.Combine(AppPaths.DefaultConfigDirectory, fileName);
                if (!File.Exists(defaultPath)) continue;
                sourcePath = defaultPath;
            }

            var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
            var backupFileName = $"{nameWithoutExtension}.backup.{DateTime.Now:yyyyMMddHHmmss}.json";
            var backupPath = Path.Combine(_configDirectory, backupFileName);
            File.Copy(sourcePath, backupPath, overwrite: true);
            backups.Add(backupPath);
        }

        return backups;
    }

    /// <summary>
    /// 获取最近的备份文件列表
    /// </summary>
    public IReadOnlyList<string> GetBackupFiles()
    {
        if (!Directory.Exists(_configDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_configDirectory, "appsettings*.backup.*.json")
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

            // 备份名形如 appsettings[.Role].backup.yyyyMMddHHmmss.json；
            // 必须恢复到原来的文件，否则角色文件的备份会被写进基文件而失效。
            var name = Path.GetFileName(backupPath);
            var markerIndex = name.IndexOf(".backup.", StringComparison.OrdinalIgnoreCase);
            var targetName = markerIndex > 0
                ? name[..markerIndex] + ".json"
                : "appsettings.json";
            var targetPath = Path.Combine(_configDirectory, targetName);

            File.Copy(backupPath, targetPath, overwrite: true);
            _logger.LogInformation("配置已从备份恢复: {BackupPath} → {TargetPath}", backupPath, targetPath);
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
