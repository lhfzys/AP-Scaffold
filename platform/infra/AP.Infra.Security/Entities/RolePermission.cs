using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Entities;

/// <summary>
/// 角色-权限关联
/// </summary>
[Table(Name = "sys_role_permissions")]
public class RolePermission
{
    [Column(IsPrimary = true)]
    public long RoleId { get; set; }

    [Column(IsPrimary = true)]
    public long PermissionId { get; set; }
}
