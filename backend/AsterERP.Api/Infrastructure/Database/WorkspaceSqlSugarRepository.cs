using System.Linq.Expressions;
using AsterERP.Api.Infrastructure.Repositories;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Domain.Common;
using AsterERP.Shared;
using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public sealed class WorkspaceSqlSugarRepository<TEntity>(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IRepository<TEntity>
    where TEntity : EntityBase, new()
{
    public Task<int> CountAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken cancellationToken = default)
    {
        return BuildQuery(predicate).CountAsync(cancellationToken);
    }

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return BuildQuery(predicate).AnyAsync(cancellationToken);
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var items = await BuildQuery(predicate).Take(1).ToListAsync(cancellationToken);
        return items.FirstOrDefault();
    }

    public ISugarQueryable<TEntity> Query(bool includeDeleted = false)
    {
        var db = databaseAccessor.GetCurrentDb();
        return includeDeleted ? db.Queryable<TEntity>() : db.Queryable<TEntity>().Where(entity => !entity.IsDeleted);
    }

    public async Task<PageResult<TEntity>> PageAsync(
        PageQuery pageQuery,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(predicate, includeDeleted)
            .OrderBy(entity => entity.CreatedTime, OrderByType.Desc);

        var totalCount = new RefAsync<int>();
        var items = await query.ToPageListAsync(pageQuery.PageIndex, pageQuery.PageSize, totalCount);

        return new PageResult<TEntity>(items, totalCount.Value, pageQuery.PageIndex, pageQuery.PageSize);
    }

    public async Task<GridPageResult<TEntity>> GridPageAsync(
        GridQuery gridQuery,
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var query = BuildQuery(predicate, includeDeleted)
            .OrderBy(entity => entity.CreatedTime, OrderByType.Desc);

        var pageQuery = gridQuery.ToPageQuery();
        var totalCount = new RefAsync<int>();
        var items = await query.ToPageListAsync(pageQuery.PageIndex, pageQuery.PageSize, totalCount);

        var pageResult = new PageResult<TEntity>(items, totalCount.Value, pageQuery.PageIndex, pageQuery.PageSize);
        return GridPageResult<TEntity>.FromPageResult(pageResult);
    }

    public async Task<List<TEntity>> ListAsync(
        Expression<Func<TEntity, bool>>? predicate = null,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        return await BuildQuery(predicate, includeDeleted)
            .OrderBy(entity => entity.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
    }

    public async Task<TEntity> InsertAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ApplyCreateAudit(entity);
        await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public async Task<int> InsertRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = entities.ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        foreach (var entity in items)
        {
            ApplyCreateAudit(entity);
        }

        return await databaseAccessor.GetCurrentDb().Insertable(items).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ApplyUpdateAudit(entity);
        return await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        var items = entities.ToList();
        if (items.Count == 0)
        {
            return 0;
        }

        foreach (var entity in items)
        {
            ApplyUpdateAudit(entity);
        }

        return await databaseAccessor.GetCurrentDb().Updateable(items).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<int> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var db = databaseAccessor.GetCurrentDb();
        var hits = await db.Queryable<TEntity>()
            .Where(item => item.Id == id && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);

        var entity = hits.FirstOrDefault();
        if (entity is null)
        {
            return 0;
        }

        entity.IsDeleted = true;
        entity.DeletedBy = currentUser.GetAsterErpUserId();
        entity.DeletedTime = DateTime.UtcNow;
        return await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public Task<int> DeletePhysicalAsync(string id, CancellationToken cancellationToken = default)
    {
        return databaseAccessor.GetCurrentDb()
            .Deleteable<TEntity>()
            .Where(entity => entity.Id == id)
            .ExecuteCommandAsync(cancellationToken);
    }

    private ISugarQueryable<TEntity> BuildQuery(Expression<Func<TEntity, bool>>? predicate = null, bool includeDeleted = false)
    {
        var db = databaseAccessor.GetCurrentDb();
        var query = includeDeleted ? db.Queryable<TEntity>() : db.Queryable<TEntity>().Where(entity => !entity.IsDeleted);
        return predicate is null ? query : query.Where(predicate);
    }

    private void ApplyCreateAudit(TEntity entity)
    {
        entity.CreatedBy = currentUser.GetAsterErpUserId();
        entity.CreatedTime = DateTime.UtcNow;
        entity.IsDeleted = false;
        entity.UpdatedBy = null;
        entity.UpdatedTime = null;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
    }

    private void ApplyUpdateAudit(TEntity entity)
    {
        entity.UpdatedBy = currentUser.GetAsterErpUserId();
        entity.UpdatedTime = DateTime.UtcNow;
    }
}
