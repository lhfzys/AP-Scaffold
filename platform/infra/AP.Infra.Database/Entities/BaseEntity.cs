using FreeSql.DataAnnotations;

namespace AP.Infra.Database.Entities;

/// <summary>
/// 数据库实体基类
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// 主键 ID 
    /// </summary>
    [Column(IsIdentity = true, IsPrimary = true)]
    public long Id { get; set; }

    /// <summary>
    /// 创建时间 (数据库自动维护)
    /// </summary>
    [Column(ServerTime = DateTimeKind.Local, CanUpdate = false)]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 更新时间 (每次更新自动修改)
    /// </summary>
    [Column(ServerTime = DateTimeKind.Local)]
    public DateTime UpdatedAt { get; set; }
}