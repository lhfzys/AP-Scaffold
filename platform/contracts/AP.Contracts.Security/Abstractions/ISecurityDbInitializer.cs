namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 安全模块数据库初始化器
/// </summary>
public interface ISecurityDbInitializer
{
    /// <summary>
    /// 初始化安全相关表、默认角色、权限和管理员账号
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);
}
