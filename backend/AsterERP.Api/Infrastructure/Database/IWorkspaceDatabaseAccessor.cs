using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public interface IWorkspaceDatabaseAccessor
{
    ISqlSugarClient MainDb { get; }

    ISqlSugarClient GetCurrentDb();

    ISqlSugarClient RequireApplicationDb();

    Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default);

    Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default);
}
