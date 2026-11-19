namespace AP.Contracts.Recipe.Models;

/// <summary>配方历史版本摘要。</summary>
public sealed record RecipeVersionInfo(
    long Id,
    long RecipeId,
    string Code,
    int Version,
    string Name,
    string? Description,
    string ChangeType,
    bool IsDefault,
    DateTime CreatedAt);
