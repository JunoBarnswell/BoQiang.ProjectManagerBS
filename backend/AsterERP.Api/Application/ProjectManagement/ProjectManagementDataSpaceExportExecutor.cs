using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>将整库导出接入既有项目管理 Operation 后台执行管道。</summary>
public sealed class ProjectManagementDataSpaceExportExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementDataSpaceExportService exportService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken)
    {
        var operation = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (!string.Equals(operation?.OperationType, "data-space.database-export", StringComparison.Ordinal)) return false;

        await exportService.ExecuteAsync(args.OperationId, cancellationToken);
        return true;
    }
}
