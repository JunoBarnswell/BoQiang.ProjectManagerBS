using System.Diagnostics;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementProjectUpdateService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementActivityWriter activityWriter,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementProjectUpdateService
{
    private const int MaximumBodyLength = 10_000;

    public async Task<ProjectManagementActivityResponse> CreateAsync(string projectId, ProjectManagementProjectUpdateRequest request, CancellationToken cancellationToken = default)
    {
        projectId = Required(projectId, "项目标识不能为空");
        await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        var body = Required(request.Body, "项目更新内容不能为空");
        if (body.Length > MaximumBodyLength) throw new ValidationException($"项目更新内容不能超过 {MaximumBodyLength} 个字符");

        var mutationId = NormalizeOptional(request.ClientMutationId);
        var db = databaseAccessor.GetCurrentDb();
        if (mutationId is not null)
        {
            var existing = await db.Queryable<ProjectManagementActivityEntity>()
                .Where(item => item.ProjectId == projectId && item.AggregateType == "ProjectUpdate" && item.AggregateId == mutationId)
                .Take(1)
                .ToListAsync(cancellationToken);
            if (existing.Count > 0) return Map(existing[0]);
        }

        var id = mutationId ?? Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), "ProjectUpdate", id, "posted", body,
            traceId, RequireUserId(), projectId, Source: "User", OccurredAt: now), cancellationToken);
        if (realtimePublisher is not null)
            await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(RequireTenantId(), RequireAppCode(), "ProjectUpdate", id, "project.update.posted", 1, traceId, projectId), cancellationToken);

        return new ProjectManagementActivityResponse(id, projectId, "ProjectUpdate", id, "posted", body, traceId, RequireUserId(), now, "User");
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ProjectManagementActivityResponse Map(ProjectManagementActivityEntity item) => new(item.Id, item.ProjectId, item.AggregateType, item.AggregateId, item.ActivityType, item.Summary, item.TraceId, item.ActorUserId, item.CreatedTime, "User");
}
