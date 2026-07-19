using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using System.Text.Json;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskBatchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementTaskProgressProjector? progressProjector = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null,
    ProjectManagementTaskLabelMutation? labelMutation = null,
    IProjectManagementTaskDependencyService? dependencyService = null) : IProjectManagementTaskBatchService
{
    public async Task<IReadOnlyList<ProjectManagementTaskResponse>> UpdateAsync(ProjectManagementTaskBatchUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ProjectId)) throw new ValidationException("项目不能为空");
        if (request.Items is null || request.Items.Count == 0 || request.Items.Count > 200) throw new ValidationException("批量任务数量必须在 1 到 200 之间");
        var db = databaseAccessor.GetCurrentDb();
        var project = await db.Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == request.ProjectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        if (project.Count == 0) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
        var ids = request.Items.Select(item => item.TaskId).Distinct(StringComparer.Ordinal).ToList();
        if (ids.Count != request.Items.Count) throw new ValidationException("批量任务不能重复");
        var tasks = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == request.ProjectId && ids.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        if (tasks.Count != ids.Count) throw new ValidationException("存在不属于当前项目或已删除的任务");
        foreach (var task in tasks)
            await Policy().EnsureCanManageTaskAsync(request.ProjectId, request.AssigneeUserId, task.Id, cancellationToken: cancellationToken);
        var byId = tasks.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var nextStatus = string.IsNullOrWhiteSpace(request.Status) ? null : ProjectManagementDomainRules.RequireTaskStatus(request.Status);
        var nextPriority = string.IsNullOrWhiteSpace(request.Priority) ? null : RequirePriority(request.Priority);
        if (request.UpdateMilestone) await EnsureMilestoneAsync(request.ProjectId, request.MilestoneId, cancellationToken);
        if (request.UpdateSchedule) ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "任务");
        var labelIds = request.UpdateLabels ? NormalizeLabelIds(request.LabelIds) : null;
        var canOverrideWip = request.OverrideWip && currentUser.HasAsterErpPermission(PermissionCodes.ProjectManagementTaskOverrideWip);
        if (request.OverrideWip && !canOverrideWip)
            throw new ValidationException("没有 WIP 强制绕过权限", ErrorCodes.PermissionDenied);
        if (nextStatus == ProjectManagementDomainRules.TaskInProgress && !canOverrideWip)
        {
            var activeCount = await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.ProjectId == request.ProjectId && !item.IsDeleted && item.Status == ProjectManagementDomainRules.TaskInProgress && !ids.Contains(item.Id)).CountAsync(cancellationToken);
            var adding = tasks.Count(item => item.Status != ProjectManagementDomainRules.TaskInProgress);
            var wipLimit = project[0].WipLimit;
            if (wipLimit is int limit && activeCount + adding > limit) throw new ValidationException("批量更新将超过项目 WIP 上限");
        }
        var now = DateTime.UtcNow;
        var actorUserId = User();
        foreach (var item in request.Items)
        {
            var task = byId[item.TaskId];
            if (task.VersionNo != item.VersionNo || item.VersionNo <= 0) throw new ValidationException($"任务 {task.TaskCode} 已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
            if (nextStatus is not null) ProjectManagementDomainRules.EnsureTaskStatusTransition(task.Status, nextStatus);
            task.Status = nextStatus ?? task.Status;
            task.Priority = nextPriority ?? task.Priority;
            if (request.AssigneeUserId is not null) task.AssigneeUserId = string.IsNullOrWhiteSpace(request.AssigneeUserId) ? null : request.AssigneeUserId.Trim();
            if (request.UpdateMilestone) task.MilestoneId = NormalizeOptional(request.MilestoneId);
            if (request.UpdateSchedule)
            {
                task.StartDate = request.StartDate;
                task.DueDate = request.DueDate;
            }
            task.BlockedReason = task.Status == ProjectManagementDomainRules.TaskBlocked ? task.BlockedReason ?? "批量阻塞" : null;
            task.VersionNo++; task.UpdatedBy = actorUserId; task.UpdatedTime = now;
        }
        List<ProjectManagementTaskEntity> finalTasks = [];
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(tasks).ExecuteCommandAsync(cancellationToken);
            if (labelIds is not null)
            {
                foreach (var task in tasks)
                {
                    await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(
                        db, task, labelIds, Tenant(), App(), actorUserId, now, cancellationToken);
                }
            }
            if (dependencyService is not null)
                await dependencyService.RefreshBlockedStatesAsync(request.ProjectId, cancellationToken);
            finalTasks = await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => ids.Contains(item.Id) && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            foreach (var task in finalTasks)
            {
                await WriteActivityAsync(task, cancellationToken);
                await WriteSyncAsync(task, cancellationToken);
            }

            await (progressProjector ?? new ProjectManagementTaskProgressProjector(databaseAccessor))
                .RefreshAsync(request.ProjectId, cancellationToken);
        });
        foreach (var task in finalTasks) await PublishAsync(task, cancellationToken);
        return finalTasks.Select(Map).ToList();
    }

    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private async Task EnsureMilestoneAsync(string projectId, string? milestoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(milestoneId)) return;
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
            .AnyAsync(item => item.Id == milestoneId.Trim() && item.ProjectId == projectId && !item.IsDeleted, cancellationToken))
            throw new ValidationException("里程碑不存在或不属于当前项目");
    }
    private async Task WriteActivityAsync(ProjectManagementTaskEntity task, CancellationToken cancellationToken) { if (activityWriter is not null) await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), "Task", task.Id, "batch.updated", $"批量更新任务 {task.Title}", Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), task.ProjectId), cancellationToken); }
    private async Task WriteSyncAsync(ProjectManagementTaskEntity task, CancellationToken cancellationToken) { if (syncJournalWriter is not null) await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(Tenant(), App(), "Task", task.Id, task.ProjectId, "batch.updated", task.VersionNo, JsonSerializer.Serialize(task), User(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken); }
    private async Task PublishAsync(ProjectManagementTaskEntity task, CancellationToken cancellationToken) { if (realtimePublisher is not null) await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "Task", task.Id, "task.batch-updated", task.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken); }
    private static string RequirePriority(string value) => value.Trim() is "Low" or "Medium" or "High" or "Urgent" ? value.Trim() : throw new ValidationException("任务优先级不受支持");
    private static IReadOnlyList<string> NormalizeLabelIds(IReadOnlyList<string>? labelIds)
    {
        if (labelIds is null) throw new ValidationException("批量更新标签时必须提供标签列表");
        return labelIds;
    }
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static ProjectManagementTaskResponse Map(ProjectManagementTaskEntity entity) => new(entity.Id, entity.ProjectId, entity.MilestoneId, entity.ParentTaskId, entity.TaskCode, entity.Title, entity.Description, entity.Status, entity.Priority, entity.AssigneeUserId, entity.AssigneeEmploymentId, entity.StartDate, entity.DueDate, entity.ProgressPercent, entity.Weight, entity.EstimateMinutes, entity.ActualMinutes, entity.SortOrder, entity.Depth, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime);
}
