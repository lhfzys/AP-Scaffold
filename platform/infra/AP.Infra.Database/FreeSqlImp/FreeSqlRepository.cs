using AP.Infra.Database.Abstractions;
using AP.Infra.Database.Entities;
using AP.Infra.Resilience.Factories;
using Polly;
using Polly.Registry;
using System.Linq.Expressions;

namespace AP.Infra.Database.FreeSqlImp;

/// <summary>
/// 基于 FreeSql 的通用仓储实现
/// </summary>
public class FreeSqlRepository<T> : IRepository<T> where T : BaseEntity
{
    protected readonly IFreeSql _fsql;
    private readonly ResiliencePipeline _pipeline;

    public FreeSqlRepository(IFreeSql fsql, ResiliencePipelineProvider<string>? pipelineProvider = null)
    {
        _fsql = fsql;
        // 注入失败（未注册韧性服务）时退化为无操作的 Empty 管道，保证仓储始终可用
        _pipeline = pipelineProvider is not null &&
                    pipelineProvider.TryGetPipeline(ResiliencePipelineFactory.Keys.Database, out var pipeline)
            ? pipeline
            : ResiliencePipeline.Empty;
    }

    public virtual async Task<T?> GetAsync(long id)
    {
        return await _pipeline.ExecuteAsync(
            async _ => await _fsql.Select<T>().Where(x => x.Id == id).FirstAsync());
    }

    public virtual async Task<List<T>> GetListAsync(Expression<Func<T, bool>> predicate)
    {
        return await _pipeline.ExecuteAsync(
            _ => new ValueTask<List<T>>(_fsql.Select<T>().Where(predicate).ToListAsync()));
    }

    public virtual async Task<long> InsertAsync(T entity)
    {
        return await _pipeline.ExecuteAsync(
            _ => new ValueTask<long>(_fsql.Insert(entity).ExecuteIdentityAsync()));
    }

    public virtual async Task<int> UpdateAsync(T entity)
    {
        return await _pipeline.ExecuteAsync(
            _ => new ValueTask<int>(_fsql.Update<T>().SetSource(entity).ExecuteAffrowsAsync()));
    }

    public virtual async Task<int> DeleteAsync(long id)
    {
        return await _pipeline.ExecuteAsync(
            _ => new ValueTask<int>(_fsql.Delete<T>().Where(x => x.Id == id).ExecuteAffrowsAsync()));
    }
}