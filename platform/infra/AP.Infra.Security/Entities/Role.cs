using AP.Infra.Database.Entities;
using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Entities;

/// <summary>
/// 角色实体
/// </summary>
[Table(Name = "sys_roles")]
public class Role : BaseEntity
{
    /// <summary>
    /// 角色名称（唯一）
    /// </summary>
    [Column(StringLength = 50, IsNullable = false)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 角色说明
    /// </summary>
    [Column(StringLength = 200)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// 关联用户
    /// </summary>
    [Navigate(ManyToMany = typeof(UserRole))]
    public ICollection<User> Users { get; set; } = new List<User>();

    /// <summary>
    /// 关联权限
    /// </summary>
    [Navigate(ManyToMany = typeof(RolePermission))]
    public ICollection<Permission> Permissions { get; set; } = new List<Permission>();
}
