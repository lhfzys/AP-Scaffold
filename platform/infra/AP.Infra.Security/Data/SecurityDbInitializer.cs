using AP.Contracts.Security.Abstractions;
using AP.Infra.Security.Audit;
using AP.Infra.Security.Entities;
using FreeSql;

namespace AP.Infra.Security.Data;

/// <summary>
/// 安全模块数据库初始化器
/// 首次启动时自动创建表并插入默认管理员账号
/// </summary>
public class SecurityDbInitializer : ISecurityDbInitializer
{
    private static readonly string[] DefaultPermissionCodes =
    [
        "system.view",
        "system.settings",
        "recipe.view",
        "recipe.edit",
        "recipe.switch",
        "report.view",
        "report.export",
        "user.manage",
        "role.manage",
        "audit.view",
        "device.config",
        "test.start"
    ];

    private readonly IFreeSql _freeSql;
    private readonly IPasswordHasher _passwordHasher;

    public SecurityDbInitializer(IFreeSql freeSql, IPasswordHasher passwordHasher)
    {
        _freeSql = freeSql ?? throw new ArgumentNullException(nameof(freeSql));
        _passwordHasher = passwordHasher ?? throw new ArgumentNullException(nameof(passwordHasher));
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // 1. 自动同步表结构（仅安全模块相关表）
        _freeSql.CodeFirst.SyncStructure(
            typeof(User),
            typeof(Role),
            typeof(UserRole),
            typeof(Permission),
            typeof(RolePermission),
            typeof(AuditLog));

        // 2. 初始化默认权限
        var existingPermissions = await _freeSql.Select<Permission>().ToListAsync(ct);
        var existingCodes = existingPermissions.Select(p => p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var code in DefaultPermissionCodes.Where(c => !existingCodes.Contains(c)))
        {
            await _freeSql.Insert(new Permission
            {
                Code = code,
                Name = code
            }).ExecuteIdentityAsync(ct);
        }

        // 3. 初始化默认角色
        var adminRole = await EnsureRoleAsync("Administrator", "系统管理员，拥有所有权限", ct);
        var operatorRole = await EnsureRoleAsync("Operator", "操作员，可运行生产、查看报表", ct);
        var technicianRole = await EnsureRoleAsync("Technician", "技术员，可编辑配方和参数", ct);

        // 4. 绑定角色权限
        await BindRolePermissionsAsync(adminRole.Id, DefaultPermissionCodes, ct);
        await BindRolePermissionsAsync(operatorRole.Id,
        [
            "system.view",
            "recipe.view",
            "recipe.switch",
            "report.view",
            "report.export",
            "test.start"
        ], ct);
        await BindRolePermissionsAsync(technicianRole.Id,
        [
            "system.view",
            "system.settings",
            "recipe.view",
            "recipe.edit",
            "recipe.switch",
            "report.view",
            "report.export",
            "device.config"
        ], ct);

        // 5. 初始化默认管理员账号（admin / admin123）
        var adminUser = await _freeSql.Select<User>().Where(u => u.UserName == "admin").ToOneAsync(ct);
        if (adminUser == null)
        {
            adminUser = new User
            {
                UserName = "admin",
                PasswordHash = _passwordHasher.HashPassword("admin123"),
                DisplayName = "系统管理员",
                IsEnabled = true,
                MustChangePassword = true
            };

            adminUser.Id = await _freeSql.Insert(adminUser).ExecuteIdentityAsync(ct);
            await _freeSql.Insert(new UserRole { UserId = adminUser.Id, RoleId = adminRole.Id }).ExecuteAffrowsAsync(ct);
        }
    }

    private async Task<Role> EnsureRoleAsync(string name, string description, CancellationToken ct)
    {
        var role = await _freeSql.Select<Role>().Where(r => r.Name == name).ToOneAsync(ct);
        if (role != null) return role;

        role = new Role { Name = name, Description = description };
        role.Id = await _freeSql.Insert(role).ExecuteIdentityAsync(ct);
        return role;
    }

    private async Task BindRolePermissionsAsync(long roleId, string[] permissionCodes, CancellationToken ct)
    {
        var permissions = await _freeSql.Select<Permission>()
            .Where(p => permissionCodes.Contains(p.Code))
            .ToListAsync(ct);

        var existing = await _freeSql.Select<RolePermission>()
            .Where(rp => rp.RoleId == roleId)
            .ToListAsync(ct);

        var existingPermissionIds = existing.Select(rp => rp.PermissionId).ToHashSet();

        foreach (var permission in permissions.Where(p => !existingPermissionIds.Contains(p.Id)))
        {
            await _freeSql.Insert(new RolePermission { RoleId = roleId, PermissionId = permission.Id }).ExecuteAffrowsAsync(ct);
        }
    }
}
