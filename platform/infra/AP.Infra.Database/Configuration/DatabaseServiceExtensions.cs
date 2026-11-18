using AP.Core.Enums;
using AP.Infra.Database.Abstractions;
using AP.Infra.Database.Backup;
using AP.Infra.Database.Migration;
using AP.Shared.Utilities.Constants;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Data.Common;

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

            // 宿主导出的绝对数据库路径（DataRoot），避免相对 CWD 落进 Program Files
            var databasePath = configuration["AppRuntime:DatabasePath"];
            if (!string.IsNullOrWhiteSpace(databasePath))
                connectionString = SetSqliteDataSource(connectionString, databasePath);

            var dbFilePath = ExtractSqliteFilePath(connectionString);
            if (!string.IsNullOrWhiteSpace(dbFilePath))
            {
                var dbDirectory = Path.GetDirectoryName(dbFilePath);
                if (!string.IsNullOrWhiteSpace(dbDirectory))
                    Directory.CreateDirectory(dbDirectory);
            }
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
                    PRAGMA busy_timeout = 5000;
                ");
            }
            catch (Exception ex)
            {
                // 仅记录错误，不阻断启动 (可能是权限问题)
                Log.Warning(ex, "SQLite 优化指令执行失败");
            }

        // 6. 注册单例
        services.AddSingleton<IFreeSql>(fsql);
        services.AddSingleton<IDatabaseSchemaMigrator, DatabaseSchemaMigrator>();
        services.AddSingleton<IDatabaseBackupService, DatabaseBackupService>();

        // 注册通用仓储 (可选，如果业务层习惯用 Repo 模式)
        services.AddScoped(typeof(IRepository<>), typeof(FreeSqlImp.FreeSqlRepository<>));

        return services;
    }

    /// <summary>
    /// 将连接串的 Data Source 替换为绝对路径，保留其余参数。
    /// </summary>
    private static string SetSqliteDataSource(string connectionString, string databasePath)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        builder["Data Source"] = databasePath;
        return builder.ConnectionString;
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
