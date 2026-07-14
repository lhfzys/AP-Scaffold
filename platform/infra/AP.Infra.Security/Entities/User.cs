using AP.Infra.Database.Entities;
using FreeSql.DataAnnotations;

namespace AP.Infra.Security.Entities;

/// <summary>
/// 用户实体
/// </summary>
[Table(Name = "sys_users")]
public class User : BaseEntity
{
    /// <summary>
    /// 用户名（唯一）
    /// </summary>
    [Column(StringLength = 50, IsNullable = false)]
    public string UserName { get; set; } = string.Empty;

    /// <summary>
    /// 密码哈希
    /// </summary>
    [Column(StringLength = 256, IsNullable = false)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称
    /// </summary>
    [Column(StringLength = 100)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 首次登录后是否必须修改密码
    /// </summary>
    public bool MustChangePassword { get; set; }

    /// <summary>
    /// 关联角色
    /// </summary>
    [Navigate(ManyToMany = typeof(UserRole))]
    public ICollection<Role> Roles { get; set; } = new List<Role>();
}
