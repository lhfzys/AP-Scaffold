namespace AP.Contracts.Security.Audit;

/// <summary>
/// 审计操作类型
/// </summary>
public enum AuditActionType
{
    Login,
    Logout,
    Create,
    Update,
    Delete,
    Execute,
    SwitchRecipe,
    ExportReport,
    ManualControl
}
