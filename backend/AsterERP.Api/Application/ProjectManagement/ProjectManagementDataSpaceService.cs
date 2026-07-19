using AsterERP.Api.Application.Auth;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// 项目管理平台数据空间的只读诊断。统计始终在数据库端聚合，且非平台管理员只会得到自己可见项目范围内的数字。
/// </summary>
public sealed class ProjectManagementDataSpaceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IMemoryCache memoryCache,
    IWorkspaceTransitionService? workspaceTransitionService = null) : IProjectManagementDataSpaceService
{
    private static readonly MemoryCacheEntryOptions SummaryCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(15),
        Size = 1
    };

    public async Task<ProjectManagementDataSpaceSummaryResponse> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var isPlatformAdministrator = currentUser.IsAsterErpPlatformAdmin() || currentUser.HasAsterErpPermission("*");
        var cacheKey = $"project-management:data-space:summary:{tenantId.ToLowerInvariant()}:{userId.ToLowerInvariant()}:{isPlatformAdministrator}";

        var summary = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SetOptions(SummaryCacheOptions);
            return await LoadSummaryAsync(tenantId, userId, isPlatformAdministrator, cancellationToken);
        });

        return summary ?? throw new InvalidOperationException("项目数据空间摘要缓存未返回结果");
    }

    public async Task<IReadOnlyList<ProjectManagementDataSpaceOptionResponse>> GetAvailableDataSpacesAsync(CancellationToken cancellationToken = default)
    {
        RequireSystemWorkspace();
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var appCode = ProjectManagementPlatformScope.AppCode;

        if (workspaceTransitionService is null)
        {
            return [new ProjectManagementDataSpaceOptionResponse(
                $"{tenantId}:{appCode}", tenantId, currentUser.GetAsterErpTenantName() ?? tenantId,
                appCode, "平台", "Enabled", true, true, true, null, null)];
        }

        var workspaces = await workspaceTransitionService.GetAvailableWorkspacesAsync(userId, cancellationToken);
        return workspaces.Select(item => new ProjectManagementDataSpaceOptionResponse(
                item.WorkspaceId,
                item.TenantId,
                item.TenantName,
                item.AppCode,
                item.AppName,
                item.Status,
                item.IsAvailable,
                item.IsDatabaseBound,
                string.Equals(item.TenantId, tenantId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.AppCode, appCode, StringComparison.OrdinalIgnoreCase),
                item.DisabledReason,
                ResolveHandlingRoute(item)))
            .ToList();
    }

    private async Task<ProjectManagementDataSpaceSummaryResponse> LoadSummaryAsync(
        string tenantId,
        string userId,
        bool isPlatformAdministrator,
        CancellationToken cancellationToken)
    {
        try
        {
            var db = databaseAccessor.GetProjectManagementDb();
            var counts = await ReadCountsAsync(db, tenantId, userId, isPlatformAdministrator, cancellationToken);
            var activeMaintenance = await db.Queryable<ProjectManagementOperationEntity>()
                .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode &&
                    (item.Status == "Pending" || item.Status == "Running") && !item.IsDeleted &&
                    (item.OperationType == "maintenance.workspace-validation" || item.OperationType == "backup.restore"))
                .OrderBy(item => item.StartedTime, OrderByType.Desc)
                .Select(item => item.OperationType)
                .Take(1)
                .ToListAsync(cancellationToken);
            var maintenanceOperation = activeMaintenance.FirstOrDefault();
            var canReadBackup = isPlatformAdministrator || currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementBackupManage);
            var lastBackupTime = canReadBackup
                ? (await db.Queryable<ProjectManagementBackupEntity>()
                    .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && item.Status == "Ready" && !item.IsDeleted)
                    .OrderBy(item => item.CompletedAt, OrderByType.Desc)
                    .Select(item => item.CompletedAt)
                    .Take(1)
                    .ToListAsync(cancellationToken)).FirstOrDefault()
                : null;
            var availableDataSpaces = await GetAvailableDataSpacesAsync(cancellationToken);
            var isMigrating = maintenanceOperation?.Contains("migration", StringComparison.OrdinalIgnoreCase) == true;
            var isMaintaining = !string.IsNullOrWhiteSpace(maintenanceOperation);

            return new ProjectManagementDataSpaceSummaryResponse(
                tenantId,
                ProjectManagementPlatformScope.AppCode,
                isMigrating ? "Migrating" : isMaintaining ? "Maintenance" : "Healthy",
                counts.ProjectCount,
                counts.TaskCount,
                counts.MemberCount,
                counts.MilestoneCount,
                counts.AttachmentCount,
                counts.LastActivityTime,
                "项目管理平台数据空间",
                "PlatformManaged",
                isMigrating ? "数据空间迁移正在执行，统计数据可能暂时滞后。" : isMaintaining ? "数据空间正在执行维护或恢复任务，统计数据可能暂时滞后。" : "数据空间连接正常。",
                isMaintaining ? "/platform/project-management/operations" : null,
                !isPlatformAdministrator,
                lastBackupTime,
                availableDataSpaces);
        }
        catch (Exception exception) when (exception is not ValidationException && exception is not OperationCanceledException)
        {
            return new ProjectManagementDataSpaceSummaryResponse(
                tenantId,
                ProjectManagementPlatformScope.AppCode,
                "Unavailable",
                0, 0, 0, 0, 0, null,
                "项目管理平台数据空间",
                "PlatformManaged",
                "数据空间暂时不可达，请运行工作区校验后重试。",
                "/platform/project-management/operations",
                !isPlatformAdministrator,
                null,
                []);
        }
    }

    private static async Task<DataSpaceCounts> ReadCountsAsync(
        ISqlSugarClient db,
        string tenantId,
        string userId,
        bool isPlatformAdministrator,
        CancellationToken cancellationToken)
    {
        if (isPlatformAdministrator)
        {
            var projectCount = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted).CountAsync(cancellationToken);
            var taskCount = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted).CountAsync(cancellationToken);
            var memberCount = await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && item.IsActive && !item.IsDeleted).CountAsync(cancellationToken);
            var milestoneCount = await db.Queryable<ProjectManagementMilestoneEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted).CountAsync(cancellationToken);
            var attachmentCount = await db.Queryable<ProjectManagementTaskAttachmentEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted).CountAsync(cancellationToken);
            var lastActivityTime = (await db.Queryable<ProjectManagementActivityEntity>().Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).Select(item => item.CreatedTime).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
            return new DataSpaceCounts(projectCount, taskCount, memberCount, milestoneCount, attachmentCount, lastActivityTime == default ? null : lastActivityTime);
        }

        var projectQuery = db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted)
            .Where(item => item.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                .Where(member => member.ProjectId == item.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                .Any());
        var projectCountScoped = await projectQuery.CountAsync(cancellationToken);
        var taskCountScoped = await db.Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted)
            .Where(item => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == item.ProjectId && project.TenantId == tenantId && project.AppCode == ProjectManagementPlatformScope.AppCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                        .Any()))
                .Any())
            .CountAsync(cancellationToken);
        var memberCountScoped = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && item.IsActive && !item.IsDeleted)
            .Where(item => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == item.ProjectId && project.TenantId == tenantId && project.AppCode == ProjectManagementPlatformScope.AppCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                        .Any()))
                .Any())
            .CountAsync(cancellationToken);
        var milestoneCountScoped = await db.Queryable<ProjectManagementMilestoneEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted)
            .Where(item => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == item.ProjectId && project.TenantId == tenantId && project.AppCode == ProjectManagementPlatformScope.AppCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                        .Any()))
                .Any())
            .CountAsync(cancellationToken);
        var attachmentCountScoped = await db.Queryable<ProjectManagementTaskAttachmentEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted)
            .Where(item => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == item.ProjectId && project.TenantId == tenantId && project.AppCode == ProjectManagementPlatformScope.AppCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                        .Any()))
                .Any())
            .CountAsync(cancellationToken);
        var lastActivityTimeScoped = (await db.Queryable<ProjectManagementActivityEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == ProjectManagementPlatformScope.AppCode && !item.IsDeleted)
            .Where(item => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                .Where(project => project.Id == item.ProjectId && project.TenantId == tenantId && project.AppCode == ProjectManagementPlatformScope.AppCode && !project.IsDeleted &&
                    (project.OwnerUserId == userId || SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                        .Where(member => member.ProjectId == project.Id && member.TenantId == tenantId && member.AppCode == ProjectManagementPlatformScope.AppCode && member.UserId == userId && member.IsActive && !member.IsDeleted)
                        .Any()))
                .Any())
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Select(item => item.CreatedTime)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();
        return new DataSpaceCounts(projectCountScoped, taskCountScoped, memberCountScoped, milestoneCountScoped, attachmentCountScoped, lastActivityTimeScoped == default ? null : lastActivityTimeScoped);
    }

    private static string? ResolveHandlingRoute(AsterERP.Contracts.Auth.WorkspaceResponse workspace) =>
        workspace.IsAvailable
            ? workspace.IsDatabaseBound || string.Equals(workspace.AppCode, ProjectManagementPlatformScope.AppCode, StringComparison.OrdinalIgnoreCase)
                ? null
                : "/platform/applications"
            : null;

    private void RequireSystemWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);

    private sealed record DataSpaceCounts(int ProjectCount, int TaskCount, int MemberCount, int MilestoneCount, int AttachmentCount, DateTime? LastActivityTime);
}
