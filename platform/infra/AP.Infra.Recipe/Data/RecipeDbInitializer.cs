using AP.Contracts.Recipe.Abstractions;
using AP.Contracts.Recipe.Models;

namespace AP.Infra.Recipe.Data;

/// <summary>
/// 配方模块数据库初始化器
/// </summary>
public class RecipeDbInitializer : IRecipeDbInitializer
{
    private readonly IRecipeManager _recipeManager;

    public RecipeDbInitializer(IRecipeManager recipeManager)
    {
        _recipeManager = recipeManager ?? throw new ArgumentNullException(nameof(recipeManager));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 表结构由 DatabaseSchemaMigrator 的 RecipeSchemaContributor 创建；这里只做种子。
        // 首次启动时创建一个默认配方示例
        var existing = await _recipeManager.GetByCodeAsync("DEFAULT", ct);
        if (existing != null)
        {
            await EnsureCurrentAsync(ct);
            return;
        }

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

    /// <summary>恢复上次激活的配方；没有状态时回退默认配方。</summary>
    private async Task EnsureCurrentAsync(CancellationToken ct)
    {
        var current = await _recipeManager.GetCurrentAsync(ct);
        if (current != null) return;

        var fallback = await _recipeManager.GetDefaultAsync(ct);
        if (fallback != null)
            await _recipeManager.SwitchAsync(fallback.Code, ct);
    }
}
