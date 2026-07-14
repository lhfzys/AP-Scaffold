namespace AP.Contracts.Recipe.Models;

/// <summary>
/// 配方信息
/// </summary>
public class RecipeInfo
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int Version { get; set; }

    public bool IsDefault { get; set; }

    public bool IsEnabled { get; set; }

    public List<RecipeParameter> Parameters { get; set; } = new();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
