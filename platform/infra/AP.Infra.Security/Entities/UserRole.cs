using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Entities;

/// <summary>
/// 用户-角色关联
/// </summary>
[Table(Name = "sys_user_roles")]
public class UserRole
{
    [Column(IsPrimary = true)]
    public long UserId { get; set; }

    [Column(IsPrimary = true)]
    public long RoleId { get; set; }
}
