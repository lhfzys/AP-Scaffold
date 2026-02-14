using AP.Infra.Database.Entities;
using System.Linq.Expressions;

namespace AP.Infra.Database.Abstractions;

/// <summary>
/// 通用泛型仓储接口
/// </summary>
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetAsync(long id);
    Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate);
    Task<long> InsertAsync(T entity);
    Task<int> UpdateAsync(T entity);
    Task<int> DeleteAsync(long id);
}