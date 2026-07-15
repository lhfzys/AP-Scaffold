namespace AP.Contracts.Security.Models;

/// <summary>
/// 权限信息
/// </summary>
public class PermissionInfo
{
    public long Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}
