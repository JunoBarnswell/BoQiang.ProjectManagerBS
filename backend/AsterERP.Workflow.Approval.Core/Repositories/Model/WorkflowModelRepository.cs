using SqlSugar;

namespace AsterERP.Workflow.Approval.Core.Repositories.Model;

public class WorkflowModelRepository
{
    private readonly ISqlSugarClient _db;

    public WorkflowModelRepository(ISqlSugarClient db)
    {
        _db = db;
    }

    public ISqlSugarClient Db => _db;

    public async Task<dynamic?> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<dynamic>().Where("id = @id", new { id }).SingleAsync();
    }

    public async Task<List<dynamic>> FindByModelTypeAsync(int modelType, string sort, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<dynamic>().Where("modelType = @modelType", new { modelType }).OrderBy(sort).ToListAsync(cancellationToken);
    }

    public async Task<List<dynamic>> FindByModelTypeAndFilterAsync(int modelType, string filter, string sort, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<dynamic>()
            .Where("modelType = @modelType AND (name LIKE @filter OR key LIKE @filter)", new { modelType, filter = $"%{filter}%" })
            .OrderBy(sort)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<dynamic>> FindByKeyAndTypeAsync(string key, int modelType, CancellationToken cancellationToken = default)
    {
        return await _db.Queryable<dynamic>().Where("key = @key AND modelType = @modelType", new { key, modelType }).ToListAsync(cancellationToken);
    }

    public async Task SaveAsync(dynamic model, CancellationToken cancellationToken = default)
    {
        await _db.Insertable(model).ExecuteCommandAsync(cancellationToken);
    }

    public async Task UpdateAsync(dynamic model, CancellationToken cancellationToken = default)
    {
        await _db.Updateable(model).ExecuteCommandAsync(cancellationToken);
    }

    public async Task DeleteAsync(dynamic model, CancellationToken cancellationToken = default)
    {
        await _db.Deleteable(model).ExecuteCommandAsync(cancellationToken);
    }
}
