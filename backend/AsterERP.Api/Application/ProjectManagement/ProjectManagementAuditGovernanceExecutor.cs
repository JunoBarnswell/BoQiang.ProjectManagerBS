using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAuditGovernanceExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementAuditGovernanceService governanceService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken)
    {
        var operation = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        if (!string.Equals(operation?.OperationType, "audit.governance.cleanup", StringComparison.Ordinal)) return false;
        await governanceService.ExecuteCleanupAsync(args.OperationId, cancellationToken);
        return true;
    }
}
