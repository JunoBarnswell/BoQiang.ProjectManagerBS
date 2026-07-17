using AsterERP.Domain.Common;
using System.Linq.Expressions;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Repositories;

public interface IRepository<TEntity> where TEntity : EntityBase
{
    Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

    ISugarQueryable<TEntity> Query(bool includeDeleted = false);

    Task<PageResult<TEntity>> PageAsync(
        PageQuery pageQuery,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<GridPageResult<TEntity>> GridPageAsync(
        GridQuery gridQuery,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default);

    Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<int> InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    Task<int> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<int> DeletePhysicalAsync(string id, CancellationToken cancellationToken = default);
}
