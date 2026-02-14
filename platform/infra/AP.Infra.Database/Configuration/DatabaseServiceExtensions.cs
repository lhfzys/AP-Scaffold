using AP.Core.Enums;
using AP.Infra.Database.Abstractions;
using AP.Shared.Utilities.Constants;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        }

        // 3. 构建 FreeSql 实例
        var freeSqlBuilder = new FreeSqlBuilder()
            .UseConnectionString(dbType, connectionString)
            .UseAutoSyncStructure(false)
            .UseMonitorCommand(cmd =>
            {
                // 这里可以挂钩到我们的 Serilog，但为了避免循环依赖，暂时只用 Console
                // 实际生产中可以通过 ILogger 注入来记录慢 SQL
            });

        var fsql = freeSqlBuilder.Build();

        // 4. SQLite 生产级优化 (WAL 模式)
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
                Console.WriteLine($"[警告] SQLite 优化指令执行失败: {ex.Message}");
            }

        // 5. 注册单例
        services.AddSingleton<IFreeSql>(fsql);

        // 注册通用仓储 (可选，如果业务层习惯用 Repo 模式)
        services.AddScoped(typeof(IRepository<>), typeof(FreeSqlImp.FreeSqlRepository<>));

        return services;
    }
}