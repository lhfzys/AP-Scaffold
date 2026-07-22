using System.Text.Json;
using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Models;
using FreeSql;

namespace AP.Infra.Recipe.Services;

/// <summary>
/// 配方管理器实现
/// </summary>
public class RecipeManager : IRecipeManager
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly IFreeSql _freeSql;

    public RecipeManager(IFreeSql freeSql)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
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

    public async Task<RecipeInfo> CreateAsync(RecipeInfo recipe, CancellationToken ct = default)
    {
        var entity = new Entities.Recipe
        {
            Code = recipe.Code,
            Name = recipe.Name,
            Description = recipe.Description,
            Version = 1,
            IsEnabled = recipe.IsEnabled,
            ParametersJson = JsonSerializer.Serialize(recipe.Parameters, JsonOptions)
        };

        entity.Id = await _freeSql.Insert(entity).ExecuteIdentityAsync(ct);
        return MapToInfo(entity);
    }

    public async Task<RecipeInfo?> UpdateAsync(long id, RecipeInfo recipe, CancellationToken ct = default)
    {
        var entity = await _freeSql.Select<Entities.Recipe>().Where(r => r.Id == id).ToOneAsync(ct);
        if (entity == null) return null;

        entity.Code = recipe.Code;
        entity.Name = recipe.Name;
        entity.Description = recipe.Description;
        entity.Version++;
        entity.IsEnabled = recipe.IsEnabled;
        entity.ParametersJson = JsonSerializer.Serialize(recipe.Parameters, JsonOptions);

        await _freeSql.Update<Entities.Recipe>().SetSource(entity).ExecuteAffrowsAsync(ct);
        return MapToInfo(entity);
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
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

        CurrentRecipe = recipe;
        // TODO: 发布 RecipeSwitchedEvent（配方切换事件），供业务插件订阅联动
        return true;
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
