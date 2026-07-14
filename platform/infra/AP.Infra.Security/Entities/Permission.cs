using AP.Infra.Database.Entities;
using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Entities;

/// <summary>
/// 权限实体
/// </summary>
[Table(Name = "sys_permissions")]
public class Permission : BaseEntity
{
    /// <summary>
    /// 权限代码（唯一）
    /// </summary>
    [Column(StringLength = 100, IsNullable = false)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 权限名称
    /// </summary>
    [Column(StringLength = 100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 关联角色
    /// </summary>
    [Navigate(ManyToMany = typeof(RolePermission))]
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
