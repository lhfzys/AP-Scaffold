using FreeSql.DataAnnotations;

namespace AP.Infra.Recipe.Entities;

/// <summary>配方历史版本快照（创建/更新时写入，供追溯与回滚）。</summary>
[Table(Name = "recipe_versions")]
public class RecipeVersion
{
    [Column(IsPrimary = true, IsIdentity = true)]
    public long Id { get; set; }

    public long RecipeId { get; set; }

    [Column(StringLength = 50, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    public int Version { get; set; }

    [Column(StringLength = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    [Column(StringLength = 500)]
    public string? Description { get; set; }

    [Column(DbType = "TEXT")]
    public string ParametersJson { get; set; } = "[]";

    [Column(StringLength = 20)]
    public string ChangeType { get; set; } = "Update";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool IsDefault { get; set; }
}
