using AsterERP.Api.Infrastructure.Database;
using SqlSugar;

namespace AsterERP.Api.Tests;

internal sealed class TestWorkspaceDatabaseAccessor(ISqlSugarClient db) : IWorkspaceDatabaseAccessor
{
    public ISqlSugarClient MainDb => db;

    public ISqlSugarClient GetCurrentDb() => db;

    public ISqlSugarClient RequireApplicationDb() => db;

    public Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);

    public Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default) => Task.FromResult(db);
}
