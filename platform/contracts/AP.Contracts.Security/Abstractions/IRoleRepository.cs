using AP.Contracts.Security.Models;

namespace AP.Contracts.Security.Abstractions;

/// <summary>
/// 角色仓储
/// </summary>
public interface IRoleRepository
{
    Task<RoleInfo?> GetByIdAsync(long id, CancellationToken ct = default);

    Task<RoleInfo?> GetByNameAsync(string name, CancellationToken ct = default);

    Task<IReadOnlyList<RoleInfo>> GetAllAsync(CancellationToken ct = default);

    Task CreateAsync(RoleInfo role, CancellationToken ct = default);

    Task UpdateAsync(RoleInfo role, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);
}
