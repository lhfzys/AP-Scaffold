using AP.Shared.Utilities.Paths;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Database.DatabaseBackup;

/// <summary>数据库备份条目。</summary>
public sealed record DatabaseBackupInfo(string FilePath, DateTime CreatedAt, long SizeBytes);

/// <summary>
/// SQLite 备份服务：启动前用 VACUUM INTO 生成一致性快照，校验 quick_check，并按数量保留多代。
/// 恢复为离线操作：停应用后把备份文件覆盖 local_data.db，并删除 -wal/-shm（见文档）。
/// </summary>
public interface IDatabaseBackupService
{
    /// <summary>创建备份；数据库文件不存在（首次启动）返回 null。</summary>
    string? CreateBackup();

    /// <summary>列出备份，新到旧。</summary>
    IReadOnlyList<DatabaseBackupInfo> ListBackups();
}

public sealed class DatabaseBackupService : IDatabaseBackupService
{
    private const int DefaultRetentionCount = 10;

    private readonly IFreeSql _freeSql;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseBackupService> _logger;

    public DatabaseBackupService(
        IFreeSql freeSql,
        IConfiguration configuration,
        ILogger<DatabaseBackupService> logger)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string? CreateBackup()
    {
        var databasePath = _configuration["AppRuntime:DatabasePath"];
        if (string.IsNullOrWhiteSpace(databasePath))
            databasePath = AppPaths.DatabasePath;

        if (!File.Exists(databasePath))
            return null;

        // 空库（只有 SQLite 内部表/尚未迁移）没有备份价值，避免首次启动生成 4KB 空备份。
        var tableCount = Convert.ToInt32(_freeSql.Ado.ExecuteScalar(
            "SELECT COUNT(1) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'") ?? 0);
        if (tableCount == 0)
            return null;

        var databaseDirectory = Path.GetDirectoryName(databasePath);
        if (string.IsNullOrWhiteSpace(databaseDirectory))
            throw new InvalidOperationException($"无法解析数据库目录: {databasePath}");

        var backupDirectory = Path.Combine(databaseDirectory, "backups");
        Directory.CreateDirectory(backupDirectory);

        var backupPath = BuildUniqueBackupPath(backupDirectory);
        var escapedPath = backupPath.Replace("'", "''");

        _logger.LogInformation("开始数据库备份: {Database} → {Backup}", databasePath, backupPath);

        try
        {
            _freeSql.Ado.ExecuteNonQuery($"VACUUM INTO '{escapedPath}'");
        }
        catch (Exception ex)
        {
            TryDelete(backupPath);
            throw new InvalidOperationException($"数据库备份失败（VACUUM INTO）: {backupPath}", ex);
        }

        if (!VerifyBackup(backupPath))
        {
            TryDelete(backupPath);
            throw new InvalidOperationException($"数据库备份校验失败（quick_check）: {backupPath}");
        }

        PruneOldBackups(backupDirectory);
        _logger.LogInformation("数据库备份完成并通过 quick_check: {Backup}", backupPath);
        return backupPath;
    }

    public IReadOnlyList<DatabaseBackupInfo> ListBackups()
    {
        var databasePath = _configuration["AppRuntime:DatabasePath"] ?? AppPaths.DatabasePath;
        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath) ?? AppPaths.DataRoot, "backups");
        if (!Directory.Exists(backupDirectory))
            return Array.Empty<DatabaseBackupInfo>();

        return Directory.GetFiles(backupDirectory, "local_data-*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Select(f => new DatabaseBackupInfo(f.FullName, f.CreationTime, f.Length))
            .ToList();
    }

    private static string BuildUniqueBackupPath(string backupDirectory)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(backupDirectory, $"local_data-{timestamp}.db");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(backupDirectory, $"local_data-{timestamp}-{suffix}.db");
            suffix++;
        }

        return path;
    }

    private bool VerifyBackup(string backupPath)
    {
        using var backupDb = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={backupPath}")
            .Build();

        var result = backupDb.Ado.ExecuteScalar("PRAGMA quick_check")?.ToString();
        return string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase);
    }

    private void PruneOldBackups(string backupDirectory)
    {
        var retentionCount = _configuration.GetValue<int?>("Database:Backup:RetentionCount") ?? DefaultRetentionCount;
        if (retentionCount <= 0) return;

        var backups = Directory.GetFiles(backupDirectory, "local_data-*.db")
            .Select(path => new FileInfo(path))
            .OrderByDescending(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var backup in backups.Skip(retentionCount))
        {
            try
            {
                backup.Delete();
                _logger.LogInformation("已清理旧备份: {Backup}", backup.FullName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "清理旧备份失败: {Backup}", backup.FullName);
            }
        }
    }

    private void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "清理失败的备份文件失败: {Backup}", path);
        }
    }
}
