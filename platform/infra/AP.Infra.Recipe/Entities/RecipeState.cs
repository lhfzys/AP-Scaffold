using FreeSql.DataAnnotations;

namespace AP.Infra.Recipe.Entities;

/// <summary>当前激活配方（单行持久化，重启后恢复）。</summary>
[Table(Name = "recipe_state")]
public class RecipeState
{
    [Column(IsPrimary = true, StringLength = 32)]
    public string Id { get; set; } = "current";

    [Column(StringLength = 50, IsNullable = false)]
    public string CurrentCode { get; set; } = string.Empty;

    public DateTime SwitchedAt { get; set; } = DateTime.Now;
}
