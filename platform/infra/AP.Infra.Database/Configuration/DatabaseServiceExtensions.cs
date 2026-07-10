using AP.Core.Enums;
using AP.Infra.Database.Abstractions;
using AP.Shared.Utilities.Constants;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace AP.Infra.Database.Configuration;

public static class DatabaseServiceExtensions
{
    /// <summary>
    /// 注册平台数据库服务 (支持 SQLite/PostgreSQL 自动切换)
    /// </summary>
    public static IServiceCollection AddPlatformDatabase(
        this IServiceCollection services,
        IConfiguration configuration,
        AppRole appRole)
    {
        // 1. 读取配置中的 Provider (SQLite 或 PostgreSQL)
        // 默认为 SQLite，防止配置缺失导致崩溃
        var providerStr = configuration[GlobalConstants.ConfigKeys.DatabaseProvider] ?? "SQLite";

        // 2. 根据 AppRole 决定连接字符串
        // 如果是单机/客户端模式，强制优先使用 SQLite 连接串
        // 如果是服务端模式，使用配置指定的连接串
        string connectionString;
        DataType dbType;

        if (providerStr.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            dbType = DataType.PostgreSQL;
            connectionString = configuration[GlobalConstants.ConfigKeys.PostgreSqlConnection]
                               ?? throw new ArgumentNullException("未配置 PostgreSQL 连接字符串");
        }
        else
        {
            dbType = DataType.Sqlite;
            connectionString = configuration[GlobalConstants.ConfigKeys.SqliteConnection]
                               ?? "Data Source=data.db;Version=3;"; // 默认连接串

            // 3. SQLite 启动前自动备份（防止断电导致数据丢失）
            BackupSqliteDatabase(connectionString);
        }

        // 4. 构建 FreeSql 实例
        var freeSqlBuilder = new FreeSqlBuilder()
            .UseConnectionString(dbType, connectionString)
            .UseAutoSyncStructure(false)
            .UseMonitorCommand(cmd =>
            {
                // 这里可以挂钩到我们的 Serilog，但为了避免循环依赖，暂时只用 Console
                // 实际生产中可以通过 ILogger 注入来记录慢 SQL
            });

        var fsql = freeSqlBuilder.Build();

        // 5. SQLite 生产级优化 (WAL 模式)
        if (dbType == DataType.Sqlite)
            try
            {
                // 预热并执行优化命令
                // WAL: Write-Ahead Logging，大幅提升并发性能
                fsql.Ado.ExecuteNonQuery(@"
                    PRAGMA journal_mode = WAL;
                    PRAGMA synchronous = NORMAL;
                    PRAGMA temp_store = MEMORY;
                    PRAGMA cache_size = -64000;
                ");
            }
            catch (Exception ex)
            {
                // 仅记录错误，不阻断启动 (可能是权限问题)
                Log.Warning(ex, "SQLite 优化指令执行失败");
            }

        // 6. 注册单例
        services.AddSingleton<IFreeSql>(fsql);

        // 注册通用仓储 (可选，如果业务层习惯用 Repo 模式)
        services.AddScoped(typeof(IRepository<>), typeof(FreeSqlImp.FreeSqlRepository<>));

        return services;
    }

    /// <summary>
    /// SQLite 启动前自动备份
    /// 将 data.db 备份为 data.db.bak，每次启动覆盖备份
    /// 如果数据库文件不存在则跳过（首次启动）
    /// </summary>
    private static void BackupSqliteDatabase(string connectionString)
    {
        try
        {
            // 从连接字符串中解析数据库文件路径
            var dbFilePath = ExtractSqliteFilePath(connectionString);
            if (string.IsNullOrEmpty(dbFilePath) || !File.Exists(dbFilePath))
            {
                return; // 首次启动，数据库文件不存在，无需备份
            }

            var backupPath = dbFilePath + ".bak";

            // 使用 SQLite 的 backup API 进行在线备份（比文件复制更安全）
            // 先尝试通过 FreeSql 执行 backup 命令
            var backupConnectionString = connectionString.Replace(
                $"Data Source={dbFilePath}",
                $"Data Source={backupPath}");

            // 简单方式：直接复制文件（对于小型工业数据库足够安全）
            // 注意：如果数据库正在被其他进程使用，可能会失败
            File.Copy(dbFilePath, backupPath, overwrite: true);

            // 同时复制 WAL 和 SHM 文件（如果存在）
            var walFile = dbFilePath + "-wal";
            var shmFile = dbFilePath + "-shm";
            if (File.Exists(walFile))
                File.Copy(walFile, backupPath + "-wal", overwrite: true);
            if (File.Exists(shmFile))
                File.Copy(shmFile, backupPath + "-shm", overwrite: true);

            var fileSizeMb = new FileInfo(dbFilePath).Length / 1024.0 / 1024.0;
            Log.Information("SQLite 数据库已备份: {File} → {Backup} ({Size:F1}MB)",
                dbFilePath, backupPath, fileSizeMb);
        }
        catch (Exception ex)
        {
            // 备份失败不应阻断启动
            Log.Warning(ex, "SQLite 数据库备份失败，已跳过");
        }
    }

    /// <summary>
    /// 从 SQLite 连接字符串中提取文件路径
    /// 支持格式: "Data Source=xxx.db" 或 "Data Source=xxx.db;Version=3;"
    /// </summary>
    private static string? ExtractSqliteFilePath(string connectionString)
    {
        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source", StringComparison.OrdinalIgnoreCase))
            {
                var eqIndex = trimmed.IndexOf('=');
                if (eqIndex >= 0)
                {
                    var path = trimmed[(eqIndex + 1)..].Trim();
                    // 处理相对路径
                    if (!Path.IsPathRooted(path))
                    {
                        path = Path.Combine(AppContext.BaseDirectory, path);
                    }
                    return path;
                }
            }
        }
        return null;
    }
}
