using SqlSugar;

namespace AsterERP.Api.Infrastructure.Database;

public interface IWorkspaceDatabaseAccessor
{
    ISqlSugarClient MainDb { get; }

    ISqlSugarClient GetCurrentDb();

    /// <summary>
    /// Returns the platform database used by the project-management module.
    /// Project-management data is platform-scoped and must never follow the
    /// currently selected application workspace database.
    /// </summary>
    ISqlSugarClient GetProjectManagementDb() => MainDb;

    ISqlSugarClient RequireApplicationDb();

    Task<ISqlSugarClient> GetCurrentDbAsync(CancellationToken cancellationToken = default);

    Task<ISqlSugarClient> GetProjectManagementDbAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(GetProjectManagementDb());
    }

    Task<ISqlSugarClient> RequireApplicationDbAsync(CancellationToken cancellationToken = default);
}
