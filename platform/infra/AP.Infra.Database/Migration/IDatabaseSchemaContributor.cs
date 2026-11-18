using FreeSql;

namespace AP.Infra.Database.Migration;

/// <summary>
/// 一个模块的 schema 贡献者。每个贡献者只负责自己模块的表结构；
/// 由 <see cref="DatabaseSchemaMigrator"/> 统一按版本执行并记录，避免各模块在启动时各跑各的 SyncStructure。
/// </summary>
public interface IDatabaseSchemaContributor
{
    /// <summary>唯一名称（用于 schema_migrations 记录，建议用模块名，如 security/recipe/report）。</summary>
    string Name { get; }

    /// <summary>schema 版本号；新迁移递增，不要复用已发布版本号。</summary>
    int Version { get; }

    /// <summary>执行建表/改表。必须可重复执行（幂等）。</summary>
    void Apply(IFreeSql freeSql);
}
