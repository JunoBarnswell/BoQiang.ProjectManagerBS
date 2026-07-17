using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories;

public interface IRepository<T> where T : class, new()
{
    ISqlSugarClient Db { get; }

    Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<string> InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task<List<T>> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);
}
