using AP.Infra.Database.Entities;
using AP.Infra.Database.FreeSqlImp;
using AP.Infra.Resilience.Configuration;
using FluentAssertions;
using FreeSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Polly.Registry;
using Xunit;

namespace AP.Infra.Tests.Database;

public class FreeSqlRepositoryTests : IDisposable
{
    public class SampleEntity : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
    }

    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"ap_repo_test_{Guid.NewGuid():N}.db");

    private IFreeSql CreateFreeSql()
    {
        var fsql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbFile}")
            .Build();
        fsql.CodeFirst.SyncStructure<SampleEntity>();
        return fsql;
    }

    [Fact]
    public async Task Repository_ExecutesThroughDatabaseRetryPipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ILoggerFactory>());
        services.AddPlatformResilience(new ConfigurationBuilder().Build());
        using var serviceProvider = services.BuildServiceProvider();

        using var fsql = CreateFreeSql();
        var pipelineProvider = serviceProvider.GetRequiredService<ResiliencePipelineProvider<string>>();
        var repo = new FreeSqlRepository<SampleEntity>(fsql, pipelineProvider);

        var id = await repo.InsertAsync(new SampleEntity { Name = "alpha" });
        id.Should().BeGreaterThan(0);

        var loaded = await repo.GetAsync(id);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("alpha");

        loaded.Name = "beta";
        (await repo.UpdateAsync(loaded)).Should().Be(1);

        var list = await repo.GetListAsync(x => x.Name == "beta");
        list.Should().ContainSingle();

        (await repo.DeleteAsync(id)).Should().Be(1);
        (await repo.GetAsync(id)).Should().BeNull();
    }

    [Fact]
    public async Task Repository_WithoutPipelineProvider_StillWorks()
    {
        using var fsql = CreateFreeSql();
        var repo = new FreeSqlRepository<SampleEntity>(fsql);

        var id = await repo.InsertAsync(new SampleEntity { Name = "plain" });

        (await repo.GetAsync(id)).Should().NotBeNull();
    }

    public void Dispose()
    {
        if (File.Exists(_dbFile))
        {
            File.Delete(_dbFile);
        }
    }
}
