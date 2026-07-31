using AP.Contracts.Security.Audit;

namespace AP.Plugin.AuditLog.Models;

/// <summary>
/// 审计操作类型的中文显示映射（筛选下拉与列表共用）。
/// </summary>
public static class AuditActionTypeDisplay
{
    public static string Of(AuditActionType actionType) => actionType switch
    {
        AuditActionType.Login => "登录",
        AuditActionType.Logout => "退出登录",
        AuditActionType.Create => "新建",
        AuditActionType.Update => "修改",
        AuditActionType.Delete => "删除",
        AuditActionType.Execute => "执行",
        AuditActionType.SwitchRecipe => "配方切换",
        AuditActionType.ExportReport => "报表导出",
        AuditActionType.ManualControl => "手动控制",
        AuditActionType.PasswordChanged => "修改密码",
        _ => actionType.ToString()
    };
}

/// <summary>
/// 操作类型筛选项（Value 为 null 表示"全部"）。
/// </summary>
public sealed record ActionTypeOption(AuditActionType? Value, string Display)
{
    /// <summary>全部筛选项：首项为"全部"，其余按枚举顺序。</summary>
    public static IReadOnlyList<ActionTypeOption> All { get; } = BuildOptions();

    private static List<ActionTypeOption> BuildOptions()
    {
        var options = new List<ActionTypeOption> { new(null, "全部") };
        foreach (var actionType in Enum.GetValues<AuditActionType>())
        {
            options.Add(new ActionTypeOption(actionType, AuditActionTypeDisplay.Of(actionType)));
        }
        return options;
    }
}
