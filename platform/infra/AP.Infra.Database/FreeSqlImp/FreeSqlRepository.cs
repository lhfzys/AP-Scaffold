using AP.Infra.Database.Abstractions;
using AP.Infra.Database.Entities;
using System.Linq.Expressions;

namespace AP.Infra.Database.FreeSqlImp;

/// <summary>
/// 基于 FreeSql 的通用仓储实现
/// </summary>
public class FreeSqlRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IFreeSql _fsql;

    public FreeSqlRepository(IFreeSql fsql)
    {
        _fsql = fsql;
    }

    public virtual async Task<T?> GetAsync(long id)
    {
        return await _fsql.Select<T>().Where(x => x.Id == id).FirstAsync();
    }

    public virtual async Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate)
    {
        return await _fsql.Select<T>().Where(predicate).ToListAsync();
    }

    public virtual async Task<long> InsertAsync(T entity)
    {
        return await _fsql.Insert(entity).ExecuteIdentityAsync();
    }

    public virtual async Task<int> UpdateAsync(T entity)
    {
        return await _fsql.Update<T>().SetSource(entity).ExecuteAffrowsAsync();
    }

    public virtual async Task<int> DeleteAsync(long id)
    {
        return await _fsql.Delete<T>().Where(x => x.Id == id).ExecuteAffrowsAsync();
    }
}