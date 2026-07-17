using System.Linq.Expressions;
using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories;

public class SqlSugarRepository<T> : IRepository<T> where T : class, new()
{
    public SqlSugarRepository(ISqlSugarClient db)
    {
        Db = db;
    }

    public ISqlSugarClient Db { get; }

    public virtual async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var queryable = Db.Queryable<T>();
        var entityInfo = Db.EntityMaintenance.GetEntityInfo<T>();
        var hasDelFlag = entityInfo.Columns.Any(column =>
            string.Equals(column.PropertyName, "DelFlag", StringComparison.OrdinalIgnoreCase));
        var primaryKeys = entityInfo.Columns.Where(column => column.IsPrimarykey).ToList();
        if (primaryKeys.Count == 1)
        {
            var primaryKeyName = primaryKeys[0].PropertyName;
            return hasDelFlag
                ? await queryable
                    .Where($"{primaryKeyName}=@id and DelFlag=1", new { id })
                    .FirstAsync(cancellationToken)
                : await queryable.InSingleAsync(id);
        }

        var idColumn = entityInfo.Columns.FirstOrDefault(column =>
            string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase));
        if (idColumn != null)
        {
            return hasDelFlag
                ? await queryable
                    .Where("Id=@id and DelFlag=1", new { id })
                    .FirstAsync(cancellationToken)
                : await queryable
                    .Where("Id=@id", new { id })
                    .FirstAsync(cancellationToken);
        }

        throw new InvalidOperationException($"Entity {typeof(T).Name} does not define a primary key or Id property.");
    }

    public virtual async Task<string> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        await Db.Insertable(entity).ExecuteCommandAsync(cancellationToken);

        var entityInfo = Db.EntityMaintenance.GetEntityInfo<T>();
        var primaryKeys = entityInfo.Columns.Where(column => column.IsPrimarykey).ToList();
        if (primaryKeys.Count == 1)
        {
            var primaryKeyValue = primaryKeys[0].PropertyInfo?.GetValue(entity)?.ToString();
            if (!string.IsNullOrWhiteSpace(primaryKeyValue))
            {
                return primaryKeyValue;
            }
        }

        var idColumn = entityInfo.Columns.FirstOrDefault(column =>
            string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase));
        var idValue = idColumn?.PropertyInfo?.GetValue(entity)?.ToString();
        return string.IsNullOrWhiteSpace(idValue) ? "ok" : idValue;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        var entityInfo = Db.EntityMaintenance.GetEntityInfo<T>();
        var primaryKeys = entityInfo.Columns.Where(column => column.IsPrimarykey).ToList();
        if (primaryKeys.Count == 1)
        {
            await Db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            return;
        }

        var idColumn = entityInfo.Columns.FirstOrDefault(column =>
            string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase));
        if (idColumn != null)
        {
            var idValue = idColumn.PropertyInfo?.GetValue(entity);
            if (idValue != null)
            {
                await Db.Updateable(entity)
                    .WhereColumns("Id")
                    .ExecuteCommandAsync(cancellationToken);
                return;
            }
        }

        throw new InvalidOperationException($"Entity {typeof(T).Name} does not define a primary key or Id property for updates.");
    }

    public virtual async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var entityInfo = Db.EntityMaintenance.GetEntityInfo<T>();
        var primaryKeys = entityInfo.Columns.Where(column => column.IsPrimarykey).ToList();
        if (primaryKeys.Count == 1)
        {
            await Db.Deleteable<T>().In(id).ExecuteCommandAsync(cancellationToken);
            return;
        }

        var idColumn = entityInfo.Columns.FirstOrDefault(column =>
            string.Equals(column.PropertyName, "Id", StringComparison.OrdinalIgnoreCase));
        if (idColumn != null)
        {
            await Db.Deleteable<T>()
                .Where("Id=@id", new { id })
                .ExecuteCommandAsync(cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Entity {typeof(T).Name} does not define a primary key or Id property.");
    }

    public virtual async Task<List<T>> QueryAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await Db.Queryable<T>().Where(predicate).ToListAsync(cancellationToken);
    }
}
