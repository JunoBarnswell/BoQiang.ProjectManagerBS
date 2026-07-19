using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Application.Im;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.Im;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>
/// Coordinates ProjectManagement-owned conversation links with the IM-owned group aggregate.
/// It deliberately uses a deterministic business key and a durable link state instead of
/// pretending that the application database and the IM database form one transaction.
/// </summary>
public sealed class ProjectManagementImConversationService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IImConversationService imConversationService,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementActivityWriter activityWriter,
    IProjectManagementSyncJournalWriter syncJournalWriter) : IProjectManagementImConversationService
{
    public async Task<ProjectManagementImConversationResponse?> GetAsync(string projectId, string? taskId, CancellationToken cancellationToken = default)
    {
        await accessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var link = await FindLinkAsync(projectId, NormalizeOptional(taskId), cancellationToken);
        return link is null || string.IsNullOrWhiteSpace(link.ConversationId) ? null : Map(link);
    }

    public async Task<ProjectManagementImConversationResponse> EnsureAsync(string projectId, ProjectManagementImConversationEnsureRequest request, CancellationToken cancellationToken = default)
    {
        await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        var taskId = NormalizeOptional(request.TaskId);
        var context = await LoadContextAsync(projectId, taskId, cancellationToken);
        var link = await FindLinkAsync(projectId, taskId, cancellationToken) ?? await CreateLinkAsync(context, cancellationToken);
        if (string.Equals(link.Status, "Archived", StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("关联会话已归档，不能重新启用", ErrorCodes.ParameterInvalid);
        }

        try
        {
            var conversation = await imConversationService.EnsureGroupConversationAsync(
                new ImGroupConversationRequest(link.ConversationKey, context.Title, context.MemberUserIds), cancellationToken);
            await ActivateLinkAsync(link, conversation.Id, cancellationToken);
            return Map(link);
        }
        catch (Exception exception)
        {
            await RecordLinkFailureAsync(link, exception, cancellationToken);
            throw;
        }
    }

    public async Task<ProjectManagementImConversationTargetResponse> ResolveTargetAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var normalizedConversationId = NormalizeRequired(conversationId, "会话不能为空");
        var link = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.ConversationId == normalizedConversationId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);
        var entity = link.FirstOrDefault();
        if (entity is null)
        {
            return new ProjectManagementImConversationTargetResponse(false, null, null, null);
        }

        await accessPolicy.EnsureCanViewProjectAsync(entity.ProjectId, cancellationToken);
        var active = string.Equals(entity.Status, "Active", StringComparison.OrdinalIgnoreCase);
        return new ProjectManagementImConversationTargetResponse(active, entity.ProjectId, entity.TaskId, active ? BuildTargetRoute(entity.ProjectId, entity.TaskId) : null);
    }

    public async Task SynchronizeProjectLinksAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var links = await LoadActiveLinksAsync(projectId, null, cancellationToken);
        foreach (var link in links)
        {
            await SynchronizeLinkAsync(link, null, cancellationToken);
        }
    }

    public async Task RevokeProjectMemberAsync(string projectId, string userId, CancellationToken cancellationToken = default)
    {
        var normalizedUserId = NormalizeRequired(userId, "成员不能为空");
        var links = await LoadActiveLinksAsync(projectId, null, cancellationToken);
        foreach (var link in links)
        {
            await SynchronizeLinkAsync(link, normalizedUserId, cancellationToken);
        }
    }

    public async Task RevokeTaskParticipantAsync(string taskId, string userId, CancellationToken cancellationToken = default)
    {
        var normalizedTaskId = NormalizeRequired(taskId, "任务不能为空");
        var normalizedUserId = NormalizeRequired(userId, "参与人不能为空");
        var links = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.TaskId == normalizedTaskId && item.Status == "Active" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            await SynchronizeLinkAsync(link, normalizedUserId, cancellationToken);
        }
    }

    public async Task SynchronizeTaskLinksAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var normalizedTaskId = NormalizeRequired(taskId, "任务不能为空");
        var links = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.TaskId == normalizedTaskId && item.Status == "Active" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            await SynchronizeLinkAsync(link, null, cancellationToken);
        }
    }

    public async Task ArchiveProjectLinksAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var links = await LoadActiveLinksAsync(projectId, null, cancellationToken);
        foreach (var link in links)
        {
            await ArchiveLinkAsync(link, cancellationToken);
        }
    }

    public async Task ArchiveTaskLinksAsync(IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default)
    {
        var ids = taskIds.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var links = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => ids.Contains(item.TaskId!) && item.Status == "Active" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            await ArchiveLinkAsync(link, cancellationToken);
        }
    }

    public async Task ReactivateProjectLinksAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var links = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.ProjectId == projectId && item.TaskId == null && item.Status == "Archived" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            await ReactivateLinkAsync(link, cancellationToken);
        }
    }

    public async Task ReactivateTaskLinksAsync(IReadOnlyCollection<string> taskIds, CancellationToken cancellationToken = default)
    {
        var ids = taskIds.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var links = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => ids.Contains(item.TaskId!) && item.Status == "Archived" && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var link in links)
        {
            await ReactivateLinkAsync(link, cancellationToken);
        }
    }

    private async Task SynchronizeLinkAsync(ProjectManagementImConversationLinkEntity link, string? excludedUserId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link.ConversationId))
        {
            return;
        }

        try
        {
            var context = await LoadContextAsync(link.ProjectId, link.TaskId, cancellationToken, excludedUserId);
            await imConversationService.SynchronizeGroupParticipantsAsync(link.ConversationId, context.MemberUserIds, cancellationToken);
            link.LastSyncError = null;
            link.UpdatedBy = RequireUserId();
            link.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetProjectManagementDb().Updateable(link)
                .UpdateColumns(item => new { item.LastSyncError, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            await RecordLinkFailureAsync(link, exception, cancellationToken);
            throw;
        }
    }

    private async Task ArchiveLinkAsync(ProjectManagementImConversationLinkEntity link, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link.ConversationId))
        {
            return;
        }

        await imConversationService.ArchiveGroupConversationAsync(link.ConversationId, cancellationToken);
        link.Status = "Archived";
        link.LastSyncError = null;
        link.VersionNo++;
        link.UpdatedBy = RequireUserId();
        link.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetProjectManagementDb(), async () =>
        {
            await databaseAccessor.GetProjectManagementDb().Updateable(link).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(link, "im-conversation.archived", "归档关联 IM 会话", cancellationToken);
            await WriteSyncJournalAsync(link, "im-conversation.archived", cancellationToken);
        });
    }

    private async Task ReactivateLinkAsync(ProjectManagementImConversationLinkEntity link, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(link.ConversationId))
        {
            throw new ValidationException("关联会话缺少会话标识", ErrorCodes.ParameterInvalid);
        }

        var context = await LoadContextAsync(link.ProjectId, link.TaskId, cancellationToken);
        await imConversationService.ActivateGroupConversationAsync(link.ConversationId, cancellationToken);
        await imConversationService.SynchronizeGroupParticipantsAsync(link.ConversationId, context.MemberUserIds, cancellationToken);
        link.Status = "Active";
        link.LastSyncError = null;
        link.VersionNo++;
        link.UpdatedBy = RequireUserId();
        link.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetProjectManagementDb(), async () =>
        {
            await databaseAccessor.GetProjectManagementDb().Updateable(link).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(link, "im-conversation.reactivated", "恢复关联 IM 会话", cancellationToken);
            await WriteSyncJournalAsync(link, "im-conversation.reactivated", cancellationToken);
        });
    }

    private async Task<ProjectManagementImConversationLinkEntity?> FindLinkAsync(string projectId, string? taskId, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted &&
                (taskId == null ? item.TaskId == null : item.TaskId == taskId))
            .Take(1)
            .ToListAsync(cancellationToken);
        return rows.FirstOrDefault();
    }

    private async Task<ProjectManagementImConversationLinkEntity> CreateLinkAsync(LinkContext context, CancellationToken cancellationToken)
    {
        var entity = new ProjectManagementImConversationLinkEntity
        {
            TenantId = RequireTenantId(),
            AppCode = RequireAppCode(),
            ProjectId = context.ProjectId,
            TaskId = context.TaskId,
            ConversationKey = BuildConversationKey(context.ProjectId, context.TaskId),
            MemberSource = context.TaskId is null ? "ProjectMembers" : "TaskCollaborators",
            Status = "Provisioning",
            VersionNo = 1,
            CreatedBy = RequireUserId(),
            CreatedTime = DateTime.UtcNow
        };
        try
        {
            await databaseAccessor.GetProjectManagementDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
            return entity;
        }
        catch
        {
            var existing = await FindLinkAsync(context.ProjectId, context.TaskId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            return existing;
        }
    }

    private async Task ActivateLinkAsync(ProjectManagementImConversationLinkEntity link, string conversationId, CancellationToken cancellationToken)
    {
        link.ConversationId = conversationId;
        link.Status = "Active";
        link.LastSyncError = null;
        link.VersionNo++;
        link.UpdatedBy = RequireUserId();
        link.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetProjectManagementDb(), async () =>
        {
            await databaseAccessor.GetProjectManagementDb().Updateable(link).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(link, "im-conversation.linked", "关联 IM 会话", cancellationToken);
            await WriteSyncJournalAsync(link, "im-conversation.linked", cancellationToken);
        });
    }

    private async Task RecordLinkFailureAsync(ProjectManagementImConversationLinkEntity link, Exception exception, CancellationToken cancellationToken)
    {
        link.LastSyncError = exception.Message.Length <= 500 ? exception.Message : exception.Message[..500];
        link.Status = string.IsNullOrWhiteSpace(link.ConversationId) ? "ProvisioningFailed" : "Active";
        link.UpdatedBy = RequireUserId();
        link.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetProjectManagementDb().Updateable(link)
            .UpdateColumns(item => new { item.Status, item.LastSyncError, item.UpdatedBy, item.UpdatedTime })
            .ExecuteCommandAsync(cancellationToken);
    }

    private async Task<List<ProjectManagementImConversationLinkEntity>> LoadActiveLinksAsync(string projectId, string? taskId, CancellationToken cancellationToken) =>
        await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementImConversationLinkEntity>()
            .Where(item => item.ProjectId == projectId && item.Status == "Active" && !item.IsDeleted &&
                (taskId == null || item.TaskId == taskId))
            .ToListAsync(cancellationToken);

    private async Task<LinkContext> LoadContextAsync(string projectId, string? taskId, CancellationToken cancellationToken, string? excludedUserId = null)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var project = (await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
        ProjectManagementTaskEntity? task = null;
        if (taskId is not null)
        {
            task = (await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => item.Id == taskId && item.ProjectId == projectId && !item.IsDeleted)
                .Take(1)
                .ToListAsync(cancellationToken))
                .FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
        }

        var activeMembers = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.IsActive && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var activeUserIds = activeMembers.Select(item => item.UserId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(excludedUserId))
        {
            activeUserIds.Remove(excludedUserId);
        }

        var memberUserIds = task is null
            ? activeUserIds
            : await LoadTaskConversationMembersAsync(db, project, task, activeMembers, activeUserIds, cancellationToken);
        // 创建链路必须保证操作者可进入新会话；撤权同步则绝不能因为操作者
        // 恰好是被移除的成员而把其重新加入会话。
        if (!string.Equals(RequireUserId(), excludedUserId, StringComparison.OrdinalIgnoreCase))
        {
            memberUserIds.Add(RequireUserId());
        }
        if (!string.IsNullOrWhiteSpace(excludedUserId))
        {
            memberUserIds.Remove(excludedUserId);
        }
        if (memberUserIds.Count == 0)
        {
            throw new ValidationException("关联会话没有可用成员", ErrorCodes.ParameterInvalid);
        }

        return new LinkContext(projectId, taskId, task is null ? $"项目协作 · {project.ProjectName}" : $"任务协作 · {task.Title}", memberUserIds.ToList());
    }

    private static async Task<HashSet<string>> LoadTaskConversationMembersAsync(
        ISqlSugarClient db,
        ProjectManagementProjectEntity project,
        ProjectManagementTaskEntity task,
        IReadOnlyCollection<ProjectManagementProjectMemberEntity> activeMembers,
        IReadOnlySet<string> activeUserIds,
        CancellationToken cancellationToken)
    {
        var userIds = activeMembers
            .Where(item => item.RoleCode is "Owner" or "Manager" or "Lead")
            .Select(item => item.UserId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(project.OwnerUserId) && activeUserIds.Contains(project.OwnerUserId))
        {
            userIds.Add(project.OwnerUserId);
        }
        if (!string.IsNullOrWhiteSpace(task.AssigneeUserId) && activeUserIds.Contains(task.AssigneeUserId))
        {
            userIds.Add(task.AssigneeUserId);
        }
        var participants = await db.Queryable<ProjectManagementTaskParticipantEntity>()
            .Where(item => item.TaskId == task.Id && item.ProjectId == task.ProjectId && !item.IsDeleted)
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        foreach (var participantUserId in participants.Where(activeUserIds.Contains))
        {
            userIds.Add(participantUserId);
        }

        return userIds;
    }

    private async Task WriteActivityAsync(ProjectManagementImConversationLinkEntity entity, string activityType, string summary, CancellationToken cancellationToken) =>
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), "ImConversationLink", entity.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId), cancellationToken);

    private async Task WriteSyncJournalAsync(ProjectManagementImConversationLinkEntity entity, string operation, CancellationToken cancellationToken) =>
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(
            RequireTenantId(), RequireAppCode(), "ImConversationLink", entity.Id, entity.ProjectId, operation, entity.VersionNo,
            JsonSerializer.Serialize(entity), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);

    private string BuildConversationKey(string projectId, string? taskId) =>
        $"pm:{RequireTenantId()}:{RequireAppCode()}:{projectId}:{taskId ?? "project"}";

    private static string BuildTargetRoute(string projectId, string? taskId) =>
        taskId is null
            ? $"/projects/{Uri.EscapeDataString(projectId)}/overview"
            : $"/projects/{Uri.EscapeDataString(projectId)}/tasks?taskId={Uri.EscapeDataString(taskId)}";

    private static ProjectManagementImConversationResponse Map(ProjectManagementImConversationLinkEntity entity) =>
        new(entity.Id, entity.ProjectId, entity.TaskId, entity.ConversationId ?? string.Empty, "Group", entity.TaskId is null ? "项目协作" : "任务协作", entity.Status, BuildTargetRoute(entity.ProjectId, entity.TaskId), entity.VersionNo);

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string RequireAppCode() => ProjectManagementPlatformScope.AppCode;
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static string NormalizeRequired(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message, ErrorCodes.ParameterInvalid) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record LinkContext(string ProjectId, string? TaskId, string Title, IReadOnlyCollection<string> MemberUserIds);
}
