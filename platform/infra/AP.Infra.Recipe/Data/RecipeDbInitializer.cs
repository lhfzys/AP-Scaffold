using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Models;
using FreeSql;

namespace AP.Infra.Recipe.Data;

/// <summary>
/// 配方模块数据库初始化器
/// </summary>
public class RecipeDbInitializer : IRecipeDbInitializer
{
    private readonly IFreeSql _freeSql;
    private readonly IRecipeManager _recipeManager;

    public RecipeDbInitializer(IFreeSql freeSql, IRecipeManager recipeManager)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
        _recipeManager = recipeManager ?? throw new ArgumentNullException(nameof(recipeManager));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 同步表结构
        _freeSql.CodeFirst.SyncStructure(typeof(Entities.Recipe));

        // 首次启动时创建一个默认配方示例
        var existing = await _recipeManager.GetByCodeAsync("DEFAULT", ct);
        if (existing != null) return;

        var defaultRecipe = new RecipeInfo
        {
            Code = "DEFAULT",
            Name = "默认配方",
            Description = "系统初始默认配方",
            IsDefault = true,
            IsEnabled = true,
            Parameters =
            [
                new RecipeParameter { Name = "StandardPressure", Value = "200", Unit = "kPa", Description = "标准压力" },
                new RecipeParameter { Name = "MaxLeakRate", Value = "30", Unit = "Pa", Description = "最大泄漏率" }
            ]
        };

        await _recipeManager.CreateAsync(defaultRecipe, ct);
        await _recipeManager.SwitchAsync("DEFAULT", ct);
    }
}
