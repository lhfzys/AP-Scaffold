using AP.Contracts.Security.Models;

namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 权限仓储（只读，权限代码由系统预定义）
/// </summary>
public interface IPermissionRepository
{
    Task<IReadOnlyList<PermissionInfo>> GetAllAsync(CancellationToken ct = default);

    Task<PermissionInfo?> GetByCodeAsync(string code, CancellationToken ct = default);
}
