using FreeSql;
using Microsoft.Extensions.Logging;

namespace AP.Infra.Database.Migration;

/// <summary>
/// 数据库 schema 迁移器：
/// - 用 schema_migrations 记录每个模块的迁移是否已执行；
/// - 用 PRAGMA user_version 记录全局最大版本；
/// - 失败即抛出，由宿主中止启动（不再“各模块 catch 后继续”）。
/// </summary>
public interface IDatabaseSchemaMigrator
{
    /// <summary>执行未应用的迁移；必须在任何模块初始化器/仓储使用前调用。</summary>
    void Migrate();
}

public sealed class DatabaseSchemaMigrator : IDatabaseSchemaMigrator
{
    private readonly IFreeSql _freeSql;
    private readonly IReadOnlyList<IDatabaseSchemaContributor> _contributors;
    private readonly ILogger<DatabaseSchemaMigrator> _logger;

    public DatabaseSchemaMigrator(
        IFreeSql freeSql,
        IEnumerable<IDatabaseSchemaContributor> contributors,
        ILogger<DatabaseSchemaMigrator> logger)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
        _contributors = contributors?.OrderBy(c => c.Version).ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToList()
                        ?? throw new ArgumentNullException(nameof(contributors));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Migrate()
    {
        _freeSql.Ado.ExecuteNonQuery("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                name TEXT NOT NULL,
                version INTEGER NOT NULL,
                applied_at TEXT NOT NULL,
                PRIMARY KEY (name, version)
            );
            """);

        var applied = _freeSql.Ado.Query<string>("SELECT name || '|' || version FROM schema_migrations")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var currentVersion = Convert.ToInt32(_freeSql.Ado.ExecuteScalar("PRAGMA user_version") ?? 0);
        var targetVersion = _contributors.Count == 0 ? currentVersion : _contributors.Max(c => c.Version);

        foreach (var contributor in _contributors)
        {
            if (applied.Contains($"{contributor.Name}|{contributor.Version}"))
                continue;

            _logger.LogInformation(
                "执行数据库迁移: {Name} v{Version}",
                contributor.Name,
                contributor.Version);

            contributor.Apply(_freeSql);

            _freeSql.Ado.ExecuteNonQuery(
                "INSERT INTO schema_migrations (name, version, applied_at) VALUES (@name, @version, @appliedAt)",
                new
                {
                    name = contributor.Name,
                    version = contributor.Version,
                    appliedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });

            _logger.LogInformation("数据库迁移完成: {Name} v{Version}", contributor.Name, contributor.Version);
        }

        if (targetVersion > currentVersion)
        {
            _freeSql.Ado.ExecuteNonQuery($"PRAGMA user_version = {targetVersion}");
            _logger.LogInformation("数据库 schema 版本: {From} → {To}", currentVersion, targetVersion);
        }
    }
}
