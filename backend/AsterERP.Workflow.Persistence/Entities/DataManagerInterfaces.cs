using System.Linq.Expressions;
using SqlSugar;

namespace AsterERP.Workflow.Persistence.Entities;

public interface IDataManager<T> where T : class, new()
{
    Type ManagedEntityClass { get; }
    T Create();
    Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task InsertAsync(T entity, CancellationToken cancellationToken = default);
    Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(T entity, CancellationToken cancellationToken = default);
}

public abstract class AbstractDataManager<T> : IDataManager<T> where T : class, new()
{
    protected readonly ISqlSugarClient Db;

    protected AbstractDataManager(ISqlSugarClient db)
    {
        Db = db;
    }

    public virtual Type ManagedEntityClass => typeof(T);

    public virtual T Create()
    {
        return new T();
    }

    public virtual async Task<T?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return await Db.Queryable<T>().InSingleAsync(id);
    }

    public virtual async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is IEntity iEntity) iEntity.IsInserted = true;
        await Db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public virtual async Task<T> UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is IEntity iEntity) iEntity.IsUpdated = true;
        await Db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await FindByIdAsync(id, cancellationToken);
        if (entity != null)
        {
            await DeleteAsync(entity, cancellationToken);
        }
    }

    public virtual async Task DeleteAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity is IEntity iEntity) iEntity.IsDeleted = true;
        await Db.Deleteable(entity).ExecuteCommandAsync(cancellationToken);
    }

}


