using System.Text.Json;
using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Events;
using AP.Contracts.Recipe.Models;
using AP.Infra.Recipe.Entities;
using FreeSql;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AP.Infra.Recipe.Services;

/// <summary>
/// 配方管理器实现。
/// 版本快照、默认配方唯一、当前配方持久化、切换事件都在这里收口。
/// </summary>
public class RecipeManager : IRecipeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IFreeSql _freeSql;
    private readonly IMediator? _mediator;
    private readonly ILogger<RecipeManager> _logger;

    public RecipeManager(
        IFreeSql freeSql,
        IMediator? mediator = null,
        ILogger<RecipeManager>? logger = null)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
        _mediator = mediator;
        _logger = logger ?? NullLogger<RecipeManager>.Instance;
    }

    public RecipeInfo? CurrentRecipe { get; private set; }

    public async Task<IReadOnlyList<RecipeInfo>> GetAllAsync(CancellationToken ct = default)
    {
        var recipes = await _freeSql.Select<Entities.Recipe>().ToListAsync(ct);
        return recipes.Select(MapToInfo).ToList();
    }

    public async Task<RecipeInfo?> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var recipe = await _freeSql.Select<Entities.Recipe>().Where(r => r.Code == code).ToOneAsync(ct);
        return recipe == null ? null : MapToInfo(recipe);
    }

    public async Task<RecipeInfo?> GetDefaultAsync(CancellationToken ct = default)
    {
        var recipe = await _freeSql.Select<Entities.Recipe>().Where(r => r.IsDefault).ToOneAsync(ct);
        return recipe == null ? null : MapToInfo(recipe);
    }

    public async Task<RecipeInfo?> GetCurrentAsync(CancellationToken ct = default)
    {
        var state = await _freeSql.Select<RecipeState>().Where(s => s.Id == "current").ToOneAsync(ct);
        if (state == null) return null;

        var recipe = await GetByCodeAsync(state.CurrentCode, ct);
        if (recipe is not { IsEnabled: true }) return null;

        CurrentRecipe = recipe;
        return recipe;
    }

    public async Task<RecipeInfo> CreateAsync(RecipeInfo recipe, CancellationToken ct = default)
    {
        var code = recipe.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("配方编码不能为空", nameof(recipe));

        if (await GetByCodeAsync(code, ct) != null)
            throw new InvalidOperationException($"配方编码已存在: {code}");

        var shouldBeDefault = recipe.IsDefault || await GetDefaultAsync(ct) == null;

        var entity = new Entities.Recipe
        {
            Code = code,
            Name = recipe.Name.Trim(),
            Description = recipe.Description,
            Version = 1,
            IsDefault = false,
            IsEnabled = recipe.IsEnabled,
            ParametersJson = JsonSerializer.Serialize(recipe.Parameters, JsonOptions)
        };

        entity.Id = await _freeSql.Insert(entity).ExecuteIdentityAsync(ct);

        if (shouldBeDefault)
            await SetDefaultAsync(entity.Id, ct);

        entity.IsDefault = shouldBeDefault;
        await SaveVersionAsync(entity, "Create", ct);
        return MapToInfo(entity);
    }

    public async Task<RecipeInfo?> UpdateAsync(long id, RecipeInfo recipe, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<Entities.Recipe>().Where(r => r.Id == id).ToOneAsync(ct);
        if (entity == null) return null;

        var code = recipe.Code.Trim();
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("配方编码不能为空", nameof(recipe));

        var sameCode = await GetByCodeAsync(code, ct);
        if (sameCode != null && sameCode.Id != id)
            throw new InvalidOperationException($"配方编码已存在: {code}");

        entity.Code = code;
        entity.Name = recipe.Name.Trim();
        entity.Description = recipe.Description;
        entity.Version++;
        entity.IsEnabled = recipe.IsEnabled;
        entity.ParametersJson = JsonSerializer.Serialize(recipe.Parameters, JsonOptions);

        await _freeSql.Update<Entities.Recipe>().SetSource(entity).ExecuteAffrowsAsync(ct);

        if (recipe.IsDefault && !entity.IsDefault)
            await SetDefaultAsync(id, ct);

        entity.IsDefault = entity.IsDefault || recipe.IsDefault;
        await SaveVersionAsync(entity, "Update", ct);
        return MapToInfo(entity);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<Entities.Recipe>().Where(r => r.Id == id).ToOneAsync(ct);
        if (entity == null || entity.IsDefault) return false;

        var rows = await _freeSql.Delete<Entities.Recipe>(id).ExecuteAffrowsAsync(ct);
        return rows > 0;
    }

    public async Task<bool> SetDefaultAsync(long id, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<Entities.Recipe>().Where(r => r.Id == id).ToOneAsync(ct);
        if (entity == null || !entity.IsEnabled) return false;

        await _freeSql.Update<Entities.Recipe>()
            .Set(a => a.IsDefault, false)
            .Where(r => r.IsDefault)
            .ExecuteAffrowsAsync(ct);

        await _freeSql.Update<Entities.Recipe>(id)
            .Set(a => a.IsDefault, true)
            .ExecuteAffrowsAsync(ct);

        return true;
    }

    public async Task<bool> SwitchAsync(string code, CancellationToken ct = default)
    {
        var recipe = await GetByCodeAsync(code, ct);
        if (recipe == null || !recipe.IsEnabled) return false;

        var previousCode = CurrentRecipe?.Code;
        CurrentRecipe = recipe;

        var state = await _freeSql.Select<RecipeState>().Where(s => s.Id == "current").ToOneAsync(ct);
        if (state == null)
        {
            await _freeSql.Insert(new RecipeState
            {
                Id = "current",
                CurrentCode = recipe.Code,
                SwitchedAt = DateTime.Now
            }).ExecuteAffrowsAsync(ct);
        }
        else
        {
            state.CurrentCode = recipe.Code;
            state.SwitchedAt = DateTime.Now;
            await _freeSql.Update<RecipeState>().SetSource(state).ExecuteAffrowsAsync(ct);
        }

        if (_mediator != null)
        {
            try
            {
                await _mediator.Publish(new RecipeSwitchedEvent(
                    recipe.Code, recipe.Name, recipe.Version, previousCode, DateTime.Now), ct);
            }
            catch (Exception ex)
            {
                // 事件订阅方失败不应回滚已完成的切换；记录后继续。
                _logger.LogError(ex, "发布配方切换事件失败: {Code}", recipe.Code);
            }
        }

        return true;
    }

    public async Task<IReadOnlyList<RecipeVersionInfo>> GetVersionsAsync(long recipeId, CancellationToken ct = default)
    {
        var versions = await _freeSql.Select<RecipeVersion>()
            .Where(v => v.RecipeId == recipeId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

        return versions.Select(v => new RecipeVersionInfo(
            v.Id, v.RecipeId, v.Code, v.Version, v.Name, v.Description,
            v.ChangeType, v.IsDefault, v.CreatedAt)).ToList();
    }

    private async Task SaveVersionAsync(Entities.Recipe entity, string changeType, CancellationToken ct)
    {
        await _freeSql.Insert(new RecipeVersion
        {
            RecipeId = entity.Id,
            Code = entity.Code,
            Version = entity.Version,
            Name = entity.Name,
            Description = entity.Description,
            ParametersJson = entity.ParametersJson,
            ChangeType = changeType,
            IsDefault = entity.IsDefault,
            CreatedAt = DateTime.Now
        }).ExecuteAffrowsAsync(ct);
    }

    private static RecipeInfo MapToInfo(Entities.Recipe recipe)
    {
        return new RecipeInfo
        {
            Id = recipe.Id,
            Code = recipe.Code,
            Name = recipe.Name,
            Description = recipe.Description,
            Version = recipe.Version,
            IsDefault = recipe.IsDefault,
            IsEnabled = recipe.IsEnabled,
            Parameters = JsonSerializer.Deserialize<List<RecipeParameter>>(recipe.ParametersJson, JsonOptions) ?? new List<RecipeParameter>(),
            CreatedAt = recipe.CreatedAt,
            UpdatedAt = recipe.UpdatedAt
        };
    }
}
