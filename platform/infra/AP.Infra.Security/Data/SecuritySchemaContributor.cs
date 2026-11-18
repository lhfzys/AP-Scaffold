using AP.Infra.Database.Migration;
using AP.Infra.Security.Audit;
using AP.Infra.Security.Entities;
using FreeSql;

namespace AP.Infra.Security.Data;

/// <summary>安全模块 schema：用户/角色/权限/审计表。</summary>
public sealed class SecuritySchemaContributor : IDatabaseSchemaContributor
{
    public string Name => "security";
    public int Version => 1;

    public void Apply(IFreeSql freeSql)
    {
        freeSql.CodeFirst.SyncStructure(
            typeof(User),
            typeof(Role),
            typeof(UserRole),
            typeof(Permission),
            typeof(RolePermission),
            typeof(AuditLog));
    }
}
