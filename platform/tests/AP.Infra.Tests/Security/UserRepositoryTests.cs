using AP.Infra.Security.Entities;
using AP.Infra.Security.Repositories;
using FluentAssertions;
using FreeSql;
using Xunit;

namespace AP.Infra.Tests.Security;

public class UserRepositoryTests : IDisposable
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"ap_user_test_{Guid.NewGuid():N}.db");
    private readonly IFreeSql _freeSql;

    public UserRepositoryTests()
    {
        _freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbFile}")
            .Build();
        _freeSql.CodeFirst.SyncStructure<User>();
    }

    [Fact]
    public async Task FailedLogin_ReachesThreshold_SetsLockout_ThenResetClearsIt()
    {
        var userId = await _freeSql.Insert(new User
        {
            UserName = "operator",
            PasswordHash = "hash",
            IsEnabled = true
        }).ExecuteIdentityAsync();

        var repository = new UserRepository(_freeSql);

        for (var i = 0; i < 5; i++)
            await repository.RegisterFailedLoginAsync(userId, 5, TimeSpan.FromMinutes(5));

        var lockoutUntil = await repository.GetLockoutUntilAsync(userId);
        lockoutUntil.Should().NotBeNull();
        lockoutUntil!.Value.Should().BeAfter(DateTime.Now);

        await repository.ResetFailedLoginAsync(userId);

        (await repository.GetLockoutUntilAsync(userId)).Should().BeNull();
    }

    public void Dispose()
    {
        _freeSql.Dispose();
        foreach (var path in new[] { _dbFile, _dbFile + "-wal", _dbFile + "-shm" })
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch
            {
                // 测试清理失败不影响断言结果
            }
        }
    }
}
