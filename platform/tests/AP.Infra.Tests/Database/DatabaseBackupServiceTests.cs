using AP.Infra.Database.DatabaseBackup;
using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AP.Infra.Tests.Database;

public class DatabaseBackupServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"ap_backup_{Guid.NewGuid():N}");
    private readonly string _dbFile;
    private readonly IFreeSql _freeSql;

    public DatabaseBackupServiceTests()
    {
        Directory.CreateDirectory(_root);
        _dbFile = Path.Combine(_root, "local_data.db");
        _freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbFile}")
            .Build();
        _freeSql.Ado.ExecuteNonQuery("CREATE TABLE sample (id INTEGER PRIMARY KEY, name TEXT)");
        _freeSql.Ado.ExecuteNonQuery("INSERT INTO sample (name) VALUES ('alpha')");
    }

    [Fact]
    public void CreateBackup_ProducesVerifiedCopy_AndRetainsConfiguredCount()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppRuntime:DatabasePath"] = _dbFile,
                ["Database:Backup:RetentionCount"] = "2"
            })
            .Build();

        var service = new DatabaseBackupService(
            _freeSql,
            configuration,
            NullLogger<DatabaseBackupService>.Instance);

        var first = service.CreateBackup();
        var second = service.CreateBackup();
        var third = service.CreateBackup();

        first.Should().NotBeNull();
        second.Should().NotBeNull();
        third.Should().NotBeNull();
        File.Exists(third).Should().BeTrue();
        service.ListBackups().Should().HaveCount(2);

        using var backupDb = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={third}")
            .Build();
        Convert.ToInt32(backupDb.Ado.ExecuteScalar("SELECT COUNT(1) FROM sample")).Should().Be(1);
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
