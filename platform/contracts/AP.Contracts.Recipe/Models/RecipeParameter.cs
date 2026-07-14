namespace AP.Contracts.Recipe.Models;

/// <summary>
/// 配方参数项
/// </summary>
public class RecipeParameter
{
    public string Name { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Unit { get; set; }

    public string? Description { get; set; }
}
