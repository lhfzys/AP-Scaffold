using AP.Contracts.Recipe.Events;
using AP.Contracts.Recipe.Models;
using AP.Infra.Recipe.Services;
using FluentAssertions;
using FreeSql;
using MediatR;
using NSubstitute;
using Xunit;

namespace AP.Infra.Tests.Recipe;

public class RecipeManagerTests : IAsyncLifetime
{
    private readonly string _dbFile = Path.Combine(Path.GetTempPath(), $"recipe_{Guid.NewGuid():N}.db");
    private IFreeSql _freeSql = null!;
    private RecipeManager _manager = null!;

    public Task InitializeAsync()
    {
        _freeSql = new FreeSqlBuilder()
            .UseConnectionString(DataType.Sqlite, $"Data Source={_dbFile}")
            .UseAutoSyncStructure(true)
            .Build();
        _manager = new RecipeManager(_freeSql);
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Create_FirstRecipe_BecomesDefault_AndVersionsAreRecorded()
    {
        var created = await _manager.CreateAsync(new RecipeInfo
        {
            Code = "R1",
            Name = "配方1",
            IsEnabled = true,
            Parameters = [new RecipeParameter { Name = "P", Value = "1" }]
        });

        created.IsDefault.Should().BeTrue();
        (await _manager.GetDefaultAsync())!.Code.Should().Be("R1");

        var versions = await _manager.GetVersionsAsync(created.Id);
        versions.Should().ContainSingle().Which.ChangeType.Should().Be("Create");
    }

    [Fact]
    public async Task Switch_PersistsCurrentRecipe_AndPublishesEvent()
    {
        var r1 = await _manager.CreateAsync(new RecipeInfo { Code = "R1", Name = "配方1", IsEnabled = true });
        var r2 = await _manager.CreateAsync(new RecipeInfo { Code = "R2", Name = "配方2", IsEnabled = true });

        (await _manager.SwitchAsync("R2")).Should().BeTrue();

        _manager.CurrentRecipe!.Code.Should().Be("R2");

        var reopened = new RecipeManager(_freeSql);
        var current = await reopened.GetCurrentAsync();
        current!.Code.Should().Be("R2");
        r1.Id.Should().NotBe(r2.Id);
    }

    [Fact]
    public async Task Switch_PublishesRecipeSwitchedEvent()
    {
        var mediator = Substitute.For<IMediator>();
        var manager = new RecipeManager(_freeSql, mediator);
        await manager.CreateAsync(new RecipeInfo { Code = "R1", Name = "配方1", IsEnabled = true });
        await manager.CreateAsync(new RecipeInfo { Code = "R2", Name = "配方2", IsEnabled = true });

        (await manager.SwitchAsync("R2")).Should().BeTrue();

        await mediator.Received(1).Publish(
            Arg.Is<RecipeSwitchedEvent>(e => e.Code == "R2" && e.PreviousCode == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_DuplicateCode_Throws()
    {
        await _manager.CreateAsync(new RecipeInfo { Code = "R1", Name = "配方1", IsEnabled = true });

        var act = () => _manager.CreateAsync(new RecipeInfo { Code = "R1", Name = "重复", IsEnabled = true });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*已存在*");
    }

    [Fact]
    public async Task Delete_DefaultRecipe_IsRejected()
    {
        var r1 = await _manager.CreateAsync(new RecipeInfo { Code = "R1", Name = "配方1", IsEnabled = true });

        (await _manager.DeleteAsync(r1.Id)).Should().BeFalse();
    }

    public Task DisposeAsync()
    {
        _freeSql?.Dispose();
        try { if (File.Exists(_dbFile)) File.Delete(_dbFile); } catch { }
        return Task.CompletedTask;
    }
}
