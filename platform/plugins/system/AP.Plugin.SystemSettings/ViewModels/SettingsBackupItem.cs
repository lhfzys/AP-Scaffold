using System.IO;

namespace AP.Plugin.SystemSettings.ViewModels;

/// <summary>配置备份文件（用于配置中心底部“恢复备份”下拉框）。</summary>
public sealed class SettingsBackupItem
{
    public SettingsBackupItem(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public string FileName => Path.GetFileName(FilePath);
}
