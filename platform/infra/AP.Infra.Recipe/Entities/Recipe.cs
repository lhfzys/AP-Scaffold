using AP.Infra.Database.Entities;
using FreeSql.DataAnnotations;

namespace AP.Infra.Recipe.Entities;

/// <summary>
/// 配方数据库实体
/// </summary>
[Table(Name = "recipes")]
public class Recipe : BaseEntity
{
    /// <summary>
    /// 配方编码（唯一）
    /// </summary>
    [Column(StringLength = 50, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 配方名称
    /// </summary>
    [Column(StringLength = 100, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 配方说明
    /// </summary>
    [Column(StringLength = 500)]
    public string? Description { get; set; }

    /// <summary>
    /// 版本号
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// 是否为默认配方
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 配方参数（JSON）
    /// </summary>
    [Column(DbType = "TEXT")]
    public string ParametersJson { get; set; } = "[]";
}
