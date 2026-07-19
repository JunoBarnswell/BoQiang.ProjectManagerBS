using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAuditExportExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementAuditService auditService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken)
    {
        var operation = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (!string.Equals(operation?.OperationType, "audit.export", StringComparison.Ordinal)) return false;

        await auditService.ExecuteExportAsync(args.OperationId, cancellationToken);
        return true;
    }
}
