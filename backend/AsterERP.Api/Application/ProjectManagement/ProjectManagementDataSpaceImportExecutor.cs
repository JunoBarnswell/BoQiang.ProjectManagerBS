using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementDataSpaceImportExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementDataSpaceImportService importService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken)
    {
        var operation = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (!string.Equals(operation?.OperationType, "data-space.database-import", StringComparison.Ordinal)) return false;
        await importService.ExecuteAsync(args.OperationId, cancellationToken);
        return true;
    }
}
