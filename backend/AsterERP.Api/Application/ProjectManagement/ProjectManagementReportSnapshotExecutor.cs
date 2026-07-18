using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using SqlSugar;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 将报表快照与既有项目管理长任务管道统一，避免在 HTTP 请求中伪造进度。
/// </summary>
public sealed class ProjectManagementReportSnapshotExecutor(
    IWorkspaceDatabaseAccessor databaseAccessor,
    IProjectManagementReportService reportService)
{
    public async Task<bool> TryExecuteAsync(ProjectManagementOperationJobArgs args, CancellationToken cancellationToken)
    {
        var operation = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (!string.Equals(operation?.OperationType, "report.snapshot", StringComparison.Ordinal)) return false;

        await reportService.ExecuteSnapshotAsync(args.OperationId, cancellationToken);
        return true;
    }
}
