using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementPurgeFileDeletionExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementPurgeFileDeletionService deletionService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken = default)
    {
        var operation = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (operation?.OperationType is not ("task.purge" or "project.purge")) return false;
        await deletionService.TryProcessAsync(args.OperationId, cancellationToken);
        return true;
    }
}
