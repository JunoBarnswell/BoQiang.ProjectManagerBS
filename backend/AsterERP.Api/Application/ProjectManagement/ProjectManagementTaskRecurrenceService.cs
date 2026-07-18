using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Options;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

/// <summary>重复规则聚合：保存规则和任务快照，调度生成窗口；实际任务写入始终委托给任务聚合命令边界。</summary>
public sealed class ProjectManagementTaskRecurrenceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementTaskService taskService,
    IProjectManagementTaskRecurrenceScheduler scheduler,
    IOptions<ProjectManagementTaskRecurrenceOptions>? options = null,
    IProjectManagementTaskOccurrenceCommandService? occurrenceCommandService = null,
    ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementTaskRecurrenceService
{
    private readonly ProjectManagementTaskRecurrenceOptions _options = options?.Value ?? new ProjectManagementTaskRecurrenceOptions();

    public async Task<IReadOnlyList<ProjectManagementTaskRecurrenceResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await Policy().EnsureCanViewProjectAsync(projectId, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementTaskRecurrenceResponse> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        var recurrence = await GetRequiredAsync(id, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(recurrence.ProjectId, cancellationToken);
        return Map(recurrence);
    }

    public async Task<ProjectManagementTaskRecurrenceResponse> CreateAsync(string projectId, ProjectManagementTaskRecurrenceCreateRequest request, CancellationToken cancellationToken = default)
    {
        var source = await taskService.GetAsync(Required(request.SourceTaskId, "源任务不能为空"), cancellationToken);
        if (!string.Equals(source.ProjectId, projectId, StringComparison.Ordinal)) throw new ValidationException("源任务不属于当前项目");
        await Policy().EnsureCanManageTaskAsync(projectId, source.AssigneeUserId, cancellationToken);
        var normalized = NormalizeRule(request.Rule);
        var now = DateTime.UtcNow;
        var recurrence = new ProjectManagementTaskRecurrenceEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = projectId, SourceTaskId = source.Id,
            Frequency = normalized.Frequency, Interval = normalized.Interval, DaysOfWeekJson = JsonSerializer.Serialize(normalized.DaysOfWeek),
            DayOfMonth = normalized.DayOfMonth, CustomUnit = normalized.CustomUnit, StartAtLocal = normalized.StartAtLocal,
            EndsAtLocal = normalized.EndsAtLocal, TimeZoneId = normalized.TimeZoneId, GenerationWindowDays = normalized.GenerationWindowDays,
            TaskSnapshotJson = JsonSerializer.Serialize(ToSnapshot(source)), SeriesOwnerUserId = User(), IsActive = true,
            VersionNo = 1, CreatedBy = User(), CreatedTime = now
        };
        await databaseAccessor.GetCurrentDb().Insertable(recurrence).ExecuteCommandAsync(cancellationToken);
        await scheduler.ScheduleAsync(ToJobArgs(recurrence), cancellationToken);
        return Map(recurrence);
    }

    public async Task<ProjectManagementTaskRecurrenceResponse> UpdateAsync(string id, ProjectManagementTaskRecurrenceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var recurrence = await GetRequiredAsync(id, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(recurrence.ProjectId, cancellationToken: cancellationToken);
        EnsureVersion(recurrence.VersionNo, request.VersionNo);
        var normalized = NormalizeRule(request.Rule);
        recurrence.Frequency = normalized.Frequency;
        recurrence.Interval = normalized.Interval;
        recurrence.DaysOfWeekJson = JsonSerializer.Serialize(normalized.DaysOfWeek);
        recurrence.DayOfMonth = normalized.DayOfMonth;
        recurrence.CustomUnit = normalized.CustomUnit;
        recurrence.StartAtLocal = normalized.StartAtLocal;
        recurrence.EndsAtLocal = normalized.EndsAtLocal;
        recurrence.TimeZoneId = normalized.TimeZoneId;
        recurrence.GenerationWindowDays = normalized.GenerationWindowDays;
        recurrence.VersionNo++;
        recurrence.UpdatedBy = User();
        recurrence.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(recurrence).ExecuteCommandAsync(cancellationToken);
        await scheduler.ScheduleAsync(ToJobArgs(recurrence), cancellationToken);
        return Map(recurrence);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskRecurrenceOccurrenceResponse>> QueryOccurrencesAsync(string id, CancellationToken cancellationToken = default)
    {
        var recurrence = await GetRequiredAsync(id, cancellationToken);
        await Policy().EnsureCanViewProjectAsync(recurrence.ProjectId, cancellationToken);
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>()
            .Where(item => item.RecurrenceId == id && !item.IsDeleted).OrderBy(item => item.ScheduledAtUtc, OrderByType.Asc).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task EditOccurrenceAsync(string recurrenceId, string occurrenceId, ProjectManagementTaskRecurrenceOccurrenceEditRequest request, CancellationToken cancellationToken = default)
    {
        var recurrence = await GetRequiredAsync(recurrenceId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(recurrence.ProjectId, cancellationToken: cancellationToken);
        EnsureVersion(recurrence.VersionNo, request.RecurrenceVersionNo);
        var occurrence = await GetOccurrenceAsync(recurrenceId, occurrenceId, cancellationToken);
        EnsureVersion(occurrence.VersionNo, request.OccurrenceVersionNo);
        var targets = await GetMutableScopeAsync(recurrence, occurrence, request.Scope, cancellationToken);
        if (targets.Count > 0) await Commands().UpdateFutureAsync(ProjectManagementTaskOccurrenceCapability.Instance, recurrence, targets, request.Task, cancellationToken);
        if (IsFutureScope(request.Scope))
        {
            recurrence.TaskSnapshotJson = JsonSerializer.Serialize(request.Task with { VersionNo = 0 });
            recurrence.VersionNo++;
            recurrence.UpdatedBy = User();
            recurrence.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetCurrentDb().Updateable(recurrence).ExecuteCommandAsync(cancellationToken);
        }
    }

    public async Task DeleteOccurrenceAsync(string recurrenceId, string occurrenceId, ProjectManagementTaskRecurrenceOccurrenceDeleteRequest request, CancellationToken cancellationToken = default)
    {
        var recurrence = await GetRequiredAsync(recurrenceId, cancellationToken);
        await Policy().EnsureCanManageTaskAsync(recurrence.ProjectId, cancellationToken: cancellationToken);
        EnsureVersion(recurrence.VersionNo, request.RecurrenceVersionNo);
        var occurrence = await GetOccurrenceAsync(recurrenceId, occurrenceId, cancellationToken);
        EnsureVersion(occurrence.VersionNo, request.OccurrenceVersionNo);
        var targets = await GetMutableScopeAsync(recurrence, occurrence, request.Scope, cancellationToken);
        if (targets.Count > 0) await Commands().DeleteFutureAsync(ProjectManagementTaskOccurrenceCapability.Instance, recurrence, targets, cancellationToken);
        if (string.Equals(request.Scope, ProjectManagementTaskRecurrenceScopes.EntireSeries, StringComparison.Ordinal))
        {
            recurrence.IsActive = false;
            recurrence.VersionNo++;
            recurrence.UpdatedBy = User();
            recurrence.UpdatedTime = DateTime.UtcNow;
            await databaseAccessor.GetCurrentDb().Updateable(recurrence).ExecuteCommandAsync(cancellationToken);
            await scheduler.DeleteAsync(recurrence.Id, cancellationToken);
        }
    }

    public async Task GenerateAsync(ProjectManagementTaskRecurrenceGenerationJobArgs args, CancellationToken cancellationToken = default)
    {
        EnsureJobContext(args);
        var recurrence = await GetRequiredAsync(args.RecurrenceId, cancellationToken);
        if (!recurrence.IsActive) return;
        var now = DateTime.UtcNow;
        var schedules = ProjectManagementTaskRecurrenceScheduleCalculator.Expand(
            recurrence.Id, recurrence.Frequency, recurrence.Interval, DeserializeDays(recurrence.DaysOfWeekJson), recurrence.DayOfMonth,
            recurrence.CustomUnit, recurrence.StartAtLocal, recurrence.EndsAtLocal, recurrence.TimeZoneId, now.AddMinutes(-5),
            now.AddDays(recurrence.GenerationWindowDays), _options.MaximumOccurrencesPerGeneration);
        var snapshot = DeserializeSnapshot(recurrence.TaskSnapshotJson);
        foreach (var schedule in schedules)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var occurrence = new ProjectManagementTaskRecurrenceOccurrenceEntity
            {
                TenantId = recurrence.TenantId, AppCode = recurrence.AppCode, ProjectId = recurrence.ProjectId, RecurrenceId = recurrence.Id,
                RecurrenceKey = schedule.RecurrenceKey, ScheduledAtLocal = schedule.LocalTime, ScheduledAtUtc = schedule.UtcTime,
                CreatedBy = recurrence.SeriesOwnerUserId, CreatedTime = now
            };
            await Commands().CreateOccurrenceAsync(ProjectManagementTaskOccurrenceCapability.Instance, recurrence, occurrence, MaterializeSnapshot(snapshot, schedule), cancellationToken);
        }
    }

    private async Task<List<ProjectManagementTaskRecurrenceOccurrenceEntity>> GetMutableScopeAsync(ProjectManagementTaskRecurrenceEntity recurrence, ProjectManagementTaskRecurrenceOccurrenceEntity occurrence, string scope, CancellationToken cancellationToken)
    {
        var normalizedScope = NormalizeScope(scope);
        var all = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>()
            .Where(item => item.RecurrenceId == recurrence.Id && !item.IsDeleted && item.State == "Generated").ToListAsync(cancellationToken);
        var eligible = normalizedScope switch
        {
            ProjectManagementTaskRecurrenceScopes.ThisOccurrence => all.Where(item => item.Id == occurrence.Id),
            ProjectManagementTaskRecurrenceScopes.ThisAndFuture => all.Where(item => item.ScheduledAtUtc >= occurrence.ScheduledAtUtc),
            ProjectManagementTaskRecurrenceScopes.EntireSeries => all,
            _ => throw new ValidationException("重复任务编辑范围无效")
        };
        var targets = eligible.ToList();
        if (targets.Count == 0) return targets;
        var taskIds = targets.Select(item => item.TaskId).Where(value => !string.IsNullOrWhiteSpace(value)).ToList();
        var completed = taskIds.Count == 0 ? new HashSet<string>(StringComparer.Ordinal) : (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => taskIds.Contains(item.Id) && !item.IsDeleted && (item.Status == ProjectManagementDomainRules.TaskDone || item.Status == ProjectManagementDomainRules.TaskCancelled))
            .ToListAsync(cancellationToken)).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        return targets.Where(item => !completed.Contains(item.TaskId)).ToList();
    }

    private async Task<ProjectManagementTaskRecurrenceEntity> GetRequiredAsync(string id, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("重复任务规则不存在", ErrorCodes.PlatformResourceNotFound);
    private async Task<ProjectManagementTaskRecurrenceOccurrenceEntity> GetOccurrenceAsync(string recurrenceId, string occurrenceId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskRecurrenceOccurrenceEntity>().Where(item => item.Id == occurrenceId && item.RecurrenceId == recurrenceId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("重复任务实例不存在", ErrorCodes.PlatformResourceNotFound);
    private IProjectManagementTaskOccurrenceCommandService Commands() => occurrenceCommandService ?? throw new InvalidOperationException("重复任务实例命令尚未由任务聚合注册");
    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private void EnsureJobContext(ProjectManagementTaskRecurrenceGenerationJobArgs args)
    {
        if (!string.Equals(Tenant(), args.TenantId, StringComparison.Ordinal) || !string.Equals(App(), args.AppCode, StringComparison.OrdinalIgnoreCase) || !string.Equals(User(), args.SeriesOwnerUserId, StringComparison.Ordinal)) throw new InvalidOperationException("重复任务作业上下文不匹配");
    }
    private static bool IsFutureScope(string scope) => NormalizeScope(scope) is ProjectManagementTaskRecurrenceScopes.ThisAndFuture or ProjectManagementTaskRecurrenceScopes.EntireSeries;
    private static string NormalizeScope(string scope) => scope?.Trim() switch { ProjectManagementTaskRecurrenceScopes.ThisOccurrence => ProjectManagementTaskRecurrenceScopes.ThisOccurrence, ProjectManagementTaskRecurrenceScopes.ThisAndFuture => ProjectManagementTaskRecurrenceScopes.ThisAndFuture, ProjectManagementTaskRecurrenceScopes.EntireSeries => ProjectManagementTaskRecurrenceScopes.EntireSeries, _ => throw new ValidationException("重复任务范围必须为 ThisOccurrence、ThisAndFuture 或 EntireSeries") };
    private NormalizedRule NormalizeRule(ProjectManagementTaskRecurrenceRuleRequest request)
    {
        var window = request.GenerationWindowDays ?? _options.DefaultGenerationWindowDays;
        if (window is < 1 or > 366 || window > _options.MaximumGenerationWindowDays) throw new ValidationException("提前生成窗口超出允许范围");
        var schedules = ProjectManagementTaskRecurrenceScheduleCalculator.Expand("validation", request.Frequency, request.Interval, request.DaysOfWeek ?? [], request.DayOfMonth, request.CustomUnit, request.StartAtLocal, request.EndsAtLocal, request.TimeZoneId, DateTime.MinValue.AddYears(1), DateTime.MinValue.AddYears(1).AddDays(1), 1);
        _ = schedules;
        var frequency = request.Frequency.Trim();
        var customUnit = string.Equals(frequency, ProjectManagementTaskRecurrenceFrequencies.Custom, StringComparison.OrdinalIgnoreCase) ? Required(request.CustomUnit, "自定义重复单位不能为空") : null;
        return new NormalizedRule(frequency, request.Interval, (request.DaysOfWeek ?? []).Distinct().OrderBy(value => value).ToArray(), request.DayOfMonth, customUnit, DateTime.SpecifyKind(request.StartAtLocal, DateTimeKind.Unspecified), request.EndsAtLocal.HasValue ? DateTime.SpecifyKind(request.EndsAtLocal.Value, DateTimeKind.Unspecified) : null, ProjectManagementTaskRecurrenceScheduleCalculator.NormalizeTimeZoneId(request.TimeZoneId), window);
    }
    private static ProjectManagementTaskUpsertRequest ToSnapshot(ProjectManagementTaskDetailResponse task) => new(task.TaskCode, task.Title, task.Description, ProjectManagementDomainRules.TaskTodo, task.Priority, task.MilestoneId, task.ParentTaskId, task.AssigneeUserId, task.AssigneeEmploymentId, task.StartDate, task.DueDate, 0, task.Weight, task.EstimateMinutes);
    private static ProjectManagementTaskUpsertRequest DeserializeSnapshot(string json) => JsonSerializer.Deserialize<ProjectManagementTaskUpsertRequest>(json) ?? throw new ValidationException("重复任务快照无效");
    private static IReadOnlyCollection<DayOfWeek> DeserializeDays(string json) => JsonSerializer.Deserialize<DayOfWeek[]>(json) ?? [];
    private static ProjectManagementTaskUpsertRequest MaterializeSnapshot(ProjectManagementTaskUpsertRequest snapshot, ProjectManagementTaskRecurrenceSchedule schedule)
    {
        var dueOffset = snapshot.StartDate.HasValue && snapshot.DueDate.HasValue ? snapshot.DueDate.Value - snapshot.StartDate.Value : TimeSpan.Zero;
        var prefix = snapshot.TaskCode.Length > 80 ? snapshot.TaskCode[..80] : snapshot.TaskCode;
        return snapshot with { TaskCode = $"{prefix}-{schedule.RecurrenceKey}", StartDate = schedule.LocalTime, DueDate = snapshot.DueDate.HasValue ? schedule.LocalTime.Add(dueOffset) : null, Status = ProjectManagementDomainRules.TaskTodo, ProgressPercent = 0, VersionNo = 0 };
    }
    private static ProjectManagementTaskRecurrenceGenerationJobArgs ToJobArgs(ProjectManagementTaskRecurrenceEntity entity) => new(entity.Id, entity.TenantId, entity.AppCode, entity.SeriesOwnerUserId);
    private static ProjectManagementTaskRecurrenceResponse Map(ProjectManagementTaskRecurrenceEntity entity) => new(entity.Id, entity.ProjectId, entity.SourceTaskId, entity.Frequency, entity.Interval, DeserializeDays(entity.DaysOfWeekJson).ToArray(), entity.DayOfMonth, entity.CustomUnit, entity.StartAtLocal, entity.EndsAtLocal, entity.TimeZoneId, entity.GenerationWindowDays, entity.IsActive, entity.VersionNo);
    private static ProjectManagementTaskRecurrenceOccurrenceResponse Map(ProjectManagementTaskRecurrenceOccurrenceEntity entity) => new(entity.Id, entity.RecurrenceId, entity.ProjectId, entity.TaskId, entity.RecurrenceKey, entity.ScheduledAtLocal, entity.ScheduledAtUtc, entity.State, entity.VersionNo);
    private static void EnsureVersion(long current, long request) { if (request <= 0 || request != current) throw new ValidationException("重复任务规则已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private sealed record NormalizedRule(string Frequency, int Interval, IReadOnlyList<DayOfWeek> DaysOfWeek, int? DayOfMonth, string? CustomUnit, DateTime StartAtLocal, DateTime? EndsAtLocal, string TimeZoneId, int GenerationWindowDays);
}
