using SqlSugar;

namespace AsterERP.Workflow.Persistence.Repositories;

public class RepositoryImplementation<T> : IRepository<T> where T : class, new()
{
    private readonly ISqlSugarClient _db;

    public RepositoryImplementation(ISqlSugarClient db)
    {
        _db = db;
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<T>().InSingleAsync(id);
    }

    public async Task<string> InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        var result = await _db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return result.ToString();
    }

    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        await _db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        await _db.Deleteable<T>().In(id).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<List<T>> QueryAsync(System.Linq.Expressions.Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<T>().Where(predicate).ToListAsync(cancellationToken);
    }
}
