namespace AP.Contracts.Recipe.Abstractions;

/// <summary>
/// 配方模块数据库初始化器
/// </summary>
public interface IRecipeDbInitializer
{
    /// <summary>
    /// 初始化配方相关表和默认数据
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
