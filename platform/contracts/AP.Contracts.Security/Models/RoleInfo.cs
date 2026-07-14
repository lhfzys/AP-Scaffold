namespace AP.Contracts.Security.Models;

/// <summary>
/// 角色信息
/// </summary>
public class RoleInfo
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
}
