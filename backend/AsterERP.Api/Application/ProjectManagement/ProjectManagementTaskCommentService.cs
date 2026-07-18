using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementTaskCommentService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementNotificationPublisher? notificationPublisher = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementTaskCommentService
{
    public async Task<IReadOnlyList<ProjectManagementTaskCommentResponse>> QueryAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await Policy(task.ProjectId, task.AssigneeUserId).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskCommentEntity>()
            .Where(item => item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return await MapManyAsync(rows, cancellationToken);
    }

    public async Task<GridPageResult<ProjectManagementTaskCommentMentionCandidateResponse>> QueryMentionCandidatesAsync(
        string taskId,
        ProjectManagementTaskCommentMentionCandidateQuery query,
        CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await Policy(task.ProjectId, task.AssigneeUserId).EnsureCanViewProjectAsync(task.ProjectId, cancellationToken);

        var keyword = Optional(query.Keyword);
        var tenantId = Tenant();
        var appCode = App();
        var db = databaseAccessor.GetCurrentDb();
        var candidates = db.Queryable<SystemUserEntity>()
            .Where(user =>
                !user.IsDeleted &&
                user.Status == "Enabled" &&
                SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                    .Where(member =>
                        member.ProjectId == task.ProjectId &&
                        member.TenantId == tenantId &&
                        member.AppCode == appCode &&
                        member.UserId == user.Id &&
                        member.IsActive &&
                        !member.IsDeleted)
                    .Any());

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            candidates = candidates.Where(user =>
                user.UserName.Contains(keyword) ||
                user.DisplayName.Contains(keyword));
        }

        var total = new RefAsync<int>();
        var users = await candidates
            .OrderBy(user => user.DisplayName, OrderByType.Asc)
            .OrderBy(user => user.UserName, OrderByType.Asc)
            .ToPageListAsync(
                Math.Max(query.PageIndex, 1),
                Math.Clamp(query.PageSize, 1, 100),
                total,
                cancellationToken);

        return new GridPageResult<ProjectManagementTaskCommentMentionCandidateResponse>
        {
            Total = total.Value,
            Items = users
                .Select(user => new ProjectManagementTaskCommentMentionCandidateResponse(user.Id, user.UserName, user.DisplayName))
                .ToList()
        };
    }

    public async Task<ProjectManagementTaskCommentResponse> CreateAsync(string taskId, ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        await Policy(task.ProjectId, task.AssigneeUserId).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        var markdown = NormalizeMarkdown(request.Markdown);
        await EnsureParentAsync(task, request.ParentCommentId, cancellationToken);
        var mentions = NormalizeMentions(request.MentionUserIds);
        await EnsureMentionsAsync(task.ProjectId, mentions, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementTaskCommentEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = task.ProjectId, TaskId = task.Id,
            ParentCommentId = Optional(request.ParentCommentId), Markdown = markdown,
            MentionUserIdsJson = JsonSerializer.Serialize(mentions), AuthorUserId = User(),
            CreatedBy = User(), CreatedTime = now, VersionNo = 1
        };
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await PublishMentionsAsync(task, entity, mentions, cancellationToken);
            await WriteActivityAsync(task, entity, "comment.created", "新增任务评论", CreateChanges(null, entity), now, cancellationToken);
        });
        await PublishInvalidationAsync(task, entity, "comment.created", cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementTaskCommentResponse> UpdateAsync(string taskId, string id, ProjectManagementTaskCommentUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务评论不存在", ErrorCodes.PlatformResourceNotFound);
        if (!string.Equals(entity.AuthorUserId, User(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("只能编辑自己发布的评论", ErrorCodes.PermissionDenied);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var before = CommentActivitySnapshot.From(entity);
        await EnsureParentAsync(task, request.ParentCommentId, cancellationToken, entity.Id);
        var mentions = NormalizeMentions(request.MentionUserIds);
        await EnsureMentionsAsync(task.ProjectId, mentions, cancellationToken);
        entity.Markdown = NormalizeMarkdown(request.Markdown);
        entity.ParentCommentId = Optional(request.ParentCommentId);
        entity.MentionUserIdsJson = JsonSerializer.Serialize(mentions);
        entity.EditedTime = DateTime.UtcNow;
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = entity.EditedTime;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await PublishMentionsAsync(task, entity, mentions, cancellationToken);
            await WriteActivityAsync(task, entity, "comment.updated", "编辑任务评论", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
        });
        await PublishInvalidationAsync(task, entity, "comment.updated", cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string taskId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var task = await GetTaskAsync(taskId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.Id == id && item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务评论不存在", ErrorCodes.PlatformResourceNotFound);
        await Policy(task.ProjectId, task.AssigneeUserId).EnsureCanManageTaskAsync(task.ProjectId, task.AssigneeUserId, cancellationToken);
        if (!string.Equals(entity.AuthorUserId, User(), StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("只能删除自己发布的评论", ErrorCodes.PermissionDenied);
        EnsureVersion(entity.VersionNo, versionNo);
        var before = CommentActivitySnapshot.From(entity);
        entity.IsDeleted = true;
        entity.DeletedBy = User();
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = User();
        entity.UpdatedTime = entity.DeletedTime;
        entity.VersionNo++;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task, entity, "comment.deleted", "删除任务评论", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
        });
        await PublishInvalidationAsync(task, entity, "comment.deleted", cancellationToken);
    }

    private async Task<ProjectManagementTaskEntity> GetTaskAsync(string taskId, CancellationToken cancellationToken)
    {
        RequireTenant(); RequireApp();
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && item.TenantId == Tenant() && item.AppCode == App() && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureParentAsync(ProjectManagementTaskEntity task, string? parentId, CancellationToken cancellationToken, string? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(parentId)) return;
        if (string.Equals(parentId, currentId, StringComparison.Ordinal)) throw new ValidationException("评论不能引用自身");
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskCommentEntity>().Where(item => item.Id == parentId && item.TenantId == Tenant() && item.AppCode == App() && item.TaskId == task.Id && !item.IsDeleted).AnyAsync(cancellationToken))
            throw new ValidationException("父评论不存在或不属于当前任务");
    }

    private async Task EnsureMentionsAsync(string projectId, IReadOnlyList<string> mentions, CancellationToken cancellationToken)
    {
        if (mentions.Count == 0) return;
        var activeMembers = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == projectId && item.IsActive && !item.IsDeleted && mentions.Contains(item.UserId))
            .Select(item => item.UserId)
            .ToListAsync(cancellationToken);
        if (activeMembers.Count != mentions.Count) throw new ValidationException("只能提及当前项目的有效成员");
    }

    private async Task PublishMentionsAsync(ProjectManagementTaskEntity task, ProjectManagementTaskCommentEntity comment, IReadOnlyList<string> mentions, CancellationToken cancellationToken)
    {
        if (notificationPublisher is null) return;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        foreach (var userId in mentions.Where(id => !string.Equals(id, User(), StringComparison.OrdinalIgnoreCase)))
        {
            await notificationPublisher.PublishAsync(new ProjectManagementNotification(Tenant(), App(), "task.comment.mentioned", userId, "任务评论提及", $"{User()} 在任务 {task.Title} 的评论中提及了你", $"/projects/{task.ProjectId}/tasks?selectedTaskId={task.Id}", traceId, task.ProjectId, task.Id), cancellationToken);
        }
    }

    private async Task WriteActivityAsync(
        ProjectManagementTaskEntity task,
        ProjectManagementTaskCommentEntity comment,
        string activityType,
        string summary,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            Tenant(), App(), "TaskComment", comment.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), task.ProjectId,
            Source: "User", FieldChanges: changes, OccurredAt: occurredAt), cancellationToken);
    }

    private async Task PublishInvalidationAsync(ProjectManagementTaskEntity task, ProjectManagementTaskCommentEntity comment, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "TaskComment", comment.Id, eventType, comment.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), task.ProjectId), cancellationToken);
    }

    private ProjectManagementAccessPolicy Policy(string projectId, string? assignee) => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private void RequireTenant() => _ = Tenant();
    private void RequireApp() => _ = App();
    private static string NormalizeMarkdown(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException("评论内容不能为空");
        var markdown = value.Trim();
        if (markdown.Length > 20000) throw new ValidationException("评论内容不能超过 20000 个字符");
        var blocked = new[] { "<script", "</script", "javascript:", "vbscript:", " onerror=", " onclick=", " onload=" };
        if (blocked.Any(item => markdown.Contains(item, StringComparison.OrdinalIgnoreCase))) throw new ValidationException("评论包含不安全内容");
        return markdown;
    }
    private static IReadOnlyList<string> NormalizeMentions(IReadOnlyList<string>? values) => (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(50).ToList();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("评论已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(CommentActivitySnapshot? before, ProjectManagementTaskCommentEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("Markdown", "评论内容", before?.Markdown, after.Markdown, isSensitive: true),
            ProjectManagementActivityChanges.Create("ParentCommentId", "回复评论", before?.ParentCommentId, after.ParentCommentId),
            ProjectManagementActivityChanges.Create("MentionUserIds", "提及成员", before?.MentionUserIdsJson, after.MentionUserIdsJson),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before?.IsDeleted, after.IsDeleted));
    private sealed record CommentActivitySnapshot(string Markdown, string? ParentCommentId, string? MentionUserIdsJson, bool IsDeleted)
    {
        public static CommentActivitySnapshot From(ProjectManagementTaskCommentEntity entity) => new(entity.Markdown, entity.ParentCommentId, entity.MentionUserIdsJson, entity.IsDeleted);
    }
    private async Task<ProjectManagementTaskCommentResponse> MapAsync(ProjectManagementTaskCommentEntity entity, CancellationToken cancellationToken)
        => (await MapManyAsync([entity], cancellationToken))[0];

    private async Task<IReadOnlyList<ProjectManagementTaskCommentResponse>> MapManyAsync(
        IReadOnlyList<ProjectManagementTaskCommentEntity> comments,
        CancellationToken cancellationToken)
    {
        var mentionedUserIds = comments
            .SelectMany(comment => DeserializeMentionUserIds(comment.MentionUserIdsJson))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mentionedUserIds.Count == 0)
        {
            return comments.Select(comment => Map(comment, [])).ToList();
        }

        var users = await databaseAccessor.GetCurrentDb().Queryable<SystemUserEntity>()
            .Where(user => mentionedUserIds.Contains(user.Id) && !user.IsDeleted)
            .ToListAsync(cancellationToken);
        var displayNames = users.ToDictionary(user => user.Id, user => user.DisplayName, StringComparer.OrdinalIgnoreCase);
        return comments.Select(comment => Map(
            comment,
            DeserializeMentionUserIds(comment.MentionUserIdsJson)
                .Where(userId => displayNames.ContainsKey(userId))
                .Select(userId => new ProjectManagementTaskCommentMentionResponse(userId, displayNames[userId]))
                .ToList())).ToList();
    }

    private static ProjectManagementTaskCommentResponse Map(
        ProjectManagementTaskCommentEntity entity,
        IReadOnlyList<ProjectManagementTaskCommentMentionResponse> mentions)
        => new(entity.Id, entity.ProjectId, entity.TaskId, entity.ParentCommentId, entity.Markdown, mentions, entity.AuthorUserId, entity.VersionNo, entity.CreatedTime, entity.EditedTime);

    private static IReadOnlyList<string> DeserializeMentionUserIds(string? json)
    {
        try
        {
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
