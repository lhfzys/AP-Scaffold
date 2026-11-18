using AP.Infra.Database.Migration;
using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AP.Infra.Tests.Database;

public class DatabaseSchemaMigratorTests : IDisposable
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"ap_migrator_{Guid.NewGuid():N}.db");
    private readonly IFreeSql _freeSql;

    public DatabaseSchemaMigratorTests()
    {
        _freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbFile}")
            .Build();
    }

    [Fact]
    public void Migrate_AppliesContributorOnce_AndSetsUserVersion()
    {
        var contributor = new CountingContributor();
        var migrator = new DatabaseSchemaMigrator(
            _freeSql,
            new[] { contributor },
            NullLogger<DatabaseSchemaMigrator>.Instance);

        migrator.Migrate();
        migrator.Migrate();

        contributor.ApplyCount.Should().Be(1);
        Convert.ToInt32(_freeSql.Ado.ExecuteScalar("PRAGMA user_version")).Should().Be(1);

        var rows = _freeSql.Ado.Query<string>("SELECT name FROM schema_migrations");
        rows.Should().ContainSingle().Which.Should().Be("test");
    }

    [Fact]
    public void Migrate_AppliesNewHigherVersion_WhenRegistered()
    {
        var v1 = new CountingContributor("module", 1);
        var v2 = new CountingContributor("module", 2);

        new DatabaseSchemaMigrator(_freeSql, new[] { v1 }, NullLogger<DatabaseSchemaMigrator>.Instance).Migrate();
        new DatabaseSchemaMigrator(_freeSql, new[] { v1, v2 }, NullLogger<DatabaseSchemaMigrator>.Instance).Migrate();

        v1.ApplyCount.Should().Be(1);
        v2.ApplyCount.Should().Be(1);
        Convert.ToInt32(_freeSql.Ado.ExecuteScalar("PRAGMA user_version")).Should().Be(2);
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        foreach (var path in new[] { _dbFile, _dbFile + "-wal", _dbFile + "-shm" })
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    private sealed class CountingContributor : IDatabaseSchemaContributor
    {
        public CountingContributor(string name = "test", int version = 1)
        {
            Name = name;
            Version = version;
        }

        public string Name { get; }
        public int Version { get; }
        public int ApplyCount { get; private set; }

        public void Apply(IFreeSql freeSql)
        {
            ApplyCount++;
            freeSql.Ado.ExecuteNonQuery($"CREATE TABLE IF NOT EXISTS test_table_{Version} (id INTEGER PRIMARY KEY)");
        }
    }
}
