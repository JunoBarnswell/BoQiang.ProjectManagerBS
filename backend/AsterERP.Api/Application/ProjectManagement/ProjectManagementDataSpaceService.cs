using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementDataSpaceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser) : IProjectManagementDataSpaceService
{
    public async Task<ProjectManagementDataSpaceSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = currentUser.GetAsterErpTenantId()?.Trim();
        var appCode = currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(appCode))
            throw new ValidationException("当前会话缺少数据空间", ErrorCodes.PermissionDenied);

        var db = databaseAccessor.GetCurrentDb();
        try
        {
            var projectCount = await db.Queryable<ProjectManagementProjectEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
            var taskCount = await db.Queryable<ProjectManagementTaskEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
            var memberCount = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => !item.IsDeleted && item.IsActive).CountAsync(cancellationToken);
            var milestoneCount = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
            var attachmentCount = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => !item.IsDeleted).CountAsync(cancellationToken);
            var lastActivity = (await db.Queryable<ProjectManagementActivityEntity>().Where(item => !item.IsDeleted).OrderBy(item => item.CreatedTime, SqlSugar.OrderByType.Desc).Select(item => item.CreatedTime).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
            return new ProjectManagementDataSpaceSummaryResponse(tenantId, appCode, "Healthy", projectCount, taskCount, memberCount, milestoneCount, attachmentCount, lastActivity == default ? null : lastActivity);
        }
        catch (Exception exception) when (exception is not ValidationException && exception is not OperationCanceledException)
        {
            return new ProjectManagementDataSpaceSummaryResponse(tenantId, appCode, "Unavailable", 0, 0, 0, 0, 0, null);
        }
    }
}
