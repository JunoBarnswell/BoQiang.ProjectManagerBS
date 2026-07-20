using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementProjectReminderService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementReminderScheduler reminderScheduler,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementProjectReminderService
{
    private const int MaximumAttempts = 3;

    public async Task<IReadOnlyList<ProjectManagementProjectReminderResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(project.Id, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectReminderEntity>()
            .Where(item => item.ProjectId == project.Id && item.RecipientUserId == User() && !item.IsDeleted)
            .OrderBy(item => item.ReminderAtUtc, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementProjectReminderResponse> CreateAsync(
        string projectId,
        ProjectManagementProjectReminderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(project.Id, cancellationToken);
        var reminderAtUtc = NormalizeReminderAt(request.ReminderAt);
        var timeZoneId = NormalizeTimeZone(request.TimeZoneId);
        var clientRequestId = Required(request.ClientRequestId, "客户端请求标识不能为空", 128);
        var db = databaseAccessor.GetCurrentDb();
        var idempotencyKey = BuildIdempotencyKey(project.Id, User(), clientRequestId);
        var existing = (await db.Queryable<ProjectManagementProjectReminderEntity>()
            .Where(item => item.IdempotencyKey == idempotencyKey && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault();
        if (existing is not null) return Map(existing);

        var now = DateTime.UtcNow;
        var entity = new ProjectManagementProjectReminderEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = project.Id, RecipientUserId = User(),
            ReminderAtUtc = reminderAtUtc, TimeZoneId = timeZoneId, Note = NormalizeNote(request.Note),
            Status = "Pending", IdempotencyKey = idempotencyKey, MaxAttempts = MaximumAttempts,
            CreatedBy = User(), CreatedTime = now, VersionNo = 1
        };
        entity.HangfireJobId = await reminderScheduler.ScheduleAsync(
            new ProjectManagementReminderJobArgs(entity.Id, Tenant(), App(), User(), entity.VersionNo, "Project"),
            new DateTimeOffset(entity.ReminderAtUtc), cancellationToken);
        try
        {
            var traceId = $"project-reminder:{entity.Id}:{entity.VersionNo}";
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                if (activityWriter is not null)
                    await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), "ProjectReminder", entity.Id, "project.reminder.created", "新增项目提醒", traceId, User(), project.Id), cancellationToken);
            });
            if (realtimePublisher is not null)
                await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "ProjectReminder", entity.Id, "project.reminder.created", entity.VersionNo, traceId, project.Id), cancellationToken);
            return Map(entity);
        }
        catch
        {
            await reminderScheduler.DeleteAsync(entity.HangfireJobId, cancellationToken);
            throw;
        }
    }

    public async Task CancelAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        var project = await GetProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(project.Id, cancellationToken);
        var entity = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectReminderEntity>()
            .Where(item => item.Id == id && item.ProjectId == project.Id && item.RecipientUserId == User() && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("项目提醒不存在", ErrorCodes.PlatformResourceNotFound);
        if (entity.VersionNo != versionNo) throw new ProjectManagementProjectReminderVersionConflictException(entity.VersionNo, versionNo);
        if (!string.Equals(entity.Status, "Pending", StringComparison.Ordinal)) return;
        await reminderScheduler.DeleteAsync(entity.HangfireJobId, cancellationToken);
        var now = DateTime.UtcNow;
        var nextVersion = entity.VersionNo + 1;
        await ProjectManagementMutationTransaction.RunAsync(databaseAccessor.GetCurrentDb(), async () =>
        {
            await databaseAccessor.GetCurrentDb().Updateable<ProjectManagementProjectReminderEntity>()
                .SetColumns(item => new ProjectManagementProjectReminderEntity { Status = "Canceled", VersionNo = nextVersion, UpdatedBy = User(), UpdatedTime = now })
                .Where(item => item.Id == entity.Id && item.VersionNo == entity.VersionNo && !item.IsDeleted)
                .ExecuteCommandAsync(cancellationToken);
        });
    }

    private async Task<ProjectManagementProjectEntity> GetProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var id = Required(projectId, "项目标识不能为空", 128);
        var project = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault();
        return project ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private static DateTime NormalizeReminderAt(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        if (utc < DateTime.UtcNow.AddSeconds(-5)) throw new ValidationException("提醒时间必须晚于当前时间");
        return utc;
    }

    private static string NormalizeTimeZone(string? value)
    {
        var id = string.IsNullOrWhiteSpace(value) ? "UTC" : value.Trim();
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(id); return id; }
        catch (TimeZoneNotFoundException) { throw new ValidationException("时区无效"); }
        catch (InvalidTimeZoneException) { throw new ValidationException("时区无效"); }
    }

    private static string? NormalizeNote(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().Length <= 500 ? value.Trim() : value.Trim()[..500];
    private static string BuildIdempotencyKey(string projectId, string userId, string requestId) => string.Join('\u001f', projectId, userId, requestId);
    private static ProjectManagementProjectReminderResponse Map(ProjectManagementProjectReminderEntity entity) => new(entity.Id, entity.ProjectId, entity.RecipientUserId, entity.ReminderAtUtc, entity.TimeZoneId, entity.Note, entity.Status, entity.AttemptCount, entity.MaxAttempts, entity.LastAttemptAt, entity.TriggeredAt, entity.LastError, entity.VersionNo, entity.CreatedTime);
    private static string Required(string? value, string message, int maxLength = 200) { if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > maxLength) throw new ValidationException(message); return value.Trim(); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
}

public sealed class ProjectManagementProjectReminderVersionConflictException(long serverVersionNo, long clientVersionNo) : Exception("项目提醒版本冲突")
{
    public object Conflict { get; } = new { serverVersionNo, clientVersionNo };
}
