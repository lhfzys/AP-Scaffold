using AP.Contracts.Recipe.Models;

namespace AP.Contracts.Recipe.Abstractions;

/// <summary>
/// 配方管理器
/// </summary>
public interface IRecipeManager
{
    /// <summary>
    /// 获取所有配方
    /// </summary>
    Task<IReadOnlyList<RecipeInfo>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// 按编码获取配方
    /// </summary>
    Task<RecipeInfo?> GetByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 获取当前默认配方
    /// </summary>
    Task<RecipeInfo?> GetDefaultAsync(CancellationToken ct = default);

    /// <summary>
    /// 创建配方
    /// </summary>
    Task<RecipeInfo> CreateAsync(RecipeInfo recipe, CancellationToken ct = default);

    /// <summary>
    /// 更新配方（创建新版本）
    /// </summary>
    Task<RecipeInfo?> UpdateAsync(long id, RecipeInfo recipe, CancellationToken ct = default);

    /// <summary>
    /// 删除配方
    /// </summary>
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 设置默认配方
    /// </summary>
    Task<bool> SetDefaultAsync(long id, CancellationToken ct = default);

    /// <summary>
    /// 切换当前使用的配方，并触发事件
    /// </summary>
    Task<bool> SwitchAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// 当前已激活的配方
    /// </summary>
    RecipeInfo? CurrentRecipe { get; }
}
