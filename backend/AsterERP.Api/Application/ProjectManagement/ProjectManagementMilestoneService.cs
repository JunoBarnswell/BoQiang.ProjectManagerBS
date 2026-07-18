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

public sealed class ProjectManagementMilestoneService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementMilestoneService
{
    public async Task<GridPageResult<ProjectManagementMilestoneResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await AccessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var total = new RefAsync<int>();
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .OrderBy(item => item.SortOrder, OrderByType.Asc)
            .OrderBy(item => item.DueDate, OrderByType.Asc)
            .ToPageListAsync(1, 500, total, cancellationToken);
        var taskRows = items.Count == 0 ? [] : await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(task => task.ProjectId == projectId && task.MilestoneId != null && !task.IsDeleted)
            .ToListAsync(cancellationToken);
        return new GridPageResult<ProjectManagementMilestoneResponse> { Total = total.Value, Items = items.Select(item => Map(item, taskRows.Where(task => task.MilestoneId == item.Id).ToList())).ToList() };
    }

    public async Task<ProjectManagementMilestoneResponse> CreateAsync(string projectId, ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var project = await EnsureProjectAsync(projectId, cancellationToken);
        await AccessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        Validate(request);
        await EnsureOwnerAsync(project, request.OwnerUserId, cancellationToken);
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementMilestoneEntity
        {
            TenantId = RequireTenantId(), AppCode = RequireAppCode(), ProjectId = projectId,
            MilestoneName = NormalizeRequired(request.MilestoneName, "里程碑名称不能为空"), OwnerUserId = NormalizeOptional(request.OwnerUserId),
            Description = NormalizeOptional(request.Description), Status = ProjectManagementDomainRules.RequireMilestoneStatus(request.Status),
            StartDate = request.StartDate, DueDate = request.DueDate, ProgressPercent = request.ProgressPercent,
            SortOrder = request.SortOrder, CreatedBy = RequireUserId(), CreatedTime = now, VersionNo = 1
        };
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "created", $"创建里程碑 {entity.MilestoneName}", CreateChanges(null, entity), now, cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
        });
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementMilestoneResponse> UpdateAsync(string projectId, string id, ProjectManagementMilestoneUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var project = await EnsureProjectAsync(projectId, cancellationToken);
        await AccessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var before = MilestoneActivitySnapshot.From(entity);
        Validate(request);
        await EnsureOwnerAsync(project, request.OwnerUserId, cancellationToken);
        entity.MilestoneName = NormalizeRequired(request.MilestoneName, "里程碑名称不能为空");
        entity.Description = NormalizeOptional(request.Description);
        entity.OwnerUserId = NormalizeOptional(request.OwnerUserId);
        var nextStatus = ProjectManagementDomainRules.RequireMilestoneStatus(request.Status);
        ProjectManagementDomainRules.EnsureMilestoneStatusTransition(entity.Status, nextStatus);
        entity.Status = nextStatus;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.CompletedAt = entity.Status == "Completed" ? entity.CompletedAt ?? DateTime.UtcNow : null;
        entity.ProgressPercent = request.ProgressPercent;
        entity.SortOrder = request.SortOrder;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新里程碑 {entity.MilestoneName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        return await MapAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await AccessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var before = MilestoneActivitySnapshot.From(entity);
        entity.IsDeleted = true;
        entity.DeletedBy = RequireUserId();
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = entity.DeletedBy;
        entity.UpdatedTime = entity.DeletedTime;
        entity.VersionNo++;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "deleted", $"删除里程碑 {entity.MilestoneName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "deleted", cancellationToken);
        });
    }

    private ProjectManagementAccessPolicy AccessPolicy => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);

    private async Task<ProjectManagementProjectEntity> EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var tenantId = RequireTenantId();
        var appCode = RequireAppCode();
        var project = (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        return project ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementMilestoneEntity> GetRequiredAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var entity = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementMilestoneEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("里程碑不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private static void Validate(ProjectManagementMilestoneUpsertRequest request)
    {
        ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "里程碑");
        ProjectManagementDomainRules.RequireProgress(request.ProgressPercent, "里程碑");
    }

    private async Task EnsureOwnerAsync(ProjectManagementProjectEntity project, string? ownerUserId, CancellationToken cancellationToken)
    {
        var normalizedOwnerUserId = NormalizeOptional(ownerUserId);
        if (normalizedOwnerUserId is null || string.Equals(normalizedOwnerUserId, project.OwnerUserId, StringComparison.OrdinalIgnoreCase)) return;
        var isActiveMember = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectMemberEntity>()
            .Where(item => item.ProjectId == project.Id && item.UserId == normalizedOwnerUserId && item.IsActive && !item.IsDeleted)
            .AnyAsync(cancellationToken);
        if (!isActiveMember) throw new ValidationException("里程碑负责人必须是项目负责人或有效项目成员");
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireAppCode() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");

    private async Task WriteActivityAsync(
        ProjectManagementMilestoneEntity entity,
        string activityType,
        string summary,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), RequireAppCode(), "Milestone", entity.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.ProjectId,
            Source: "User", FieldChanges: changes, OccurredAt: occurredAt), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(ProjectManagementMilestoneEntity entity, string operation, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenantId(), RequireAppCode(), "Milestone", entity.Id, entity.ProjectId, operation, entity.VersionNo, JsonSerializer.Serialize(entity), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }
    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("里程碑已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }

    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(MilestoneActivitySnapshot? before, ProjectManagementMilestoneEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("MilestoneName", "里程碑名称", before?.MilestoneName, after.MilestoneName),
            ProjectManagementActivityChanges.Create("Description", "里程碑描述", before?.Description, after.Description),
            ProjectManagementActivityChanges.Create("OwnerUserId", "负责人", before?.OwnerUserId, after.OwnerUserId),
            ProjectManagementActivityChanges.Create("Status", "里程碑状态", before?.Status, after.Status),
            ProjectManagementActivityChanges.Create("StartDate", "开始日期", before?.StartDate, after.StartDate),
            ProjectManagementActivityChanges.Create("DueDate", "截止日期", before?.DueDate, after.DueDate),
            ProjectManagementActivityChanges.Create("ProgressPercent", "进度", before?.ProgressPercent, after.ProgressPercent),
            ProjectManagementActivityChanges.Create("SortOrder", "排序", before?.SortOrder, after.SortOrder),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before?.IsDeleted, after.IsDeleted));

    private sealed record MilestoneActivitySnapshot(
        string MilestoneName,
        string? Description,
        string? OwnerUserId,
        string Status,
        DateTime? StartDate,
        DateTime? DueDate,
        decimal ProgressPercent,
        int SortOrder,
        bool IsDeleted)
    {
        public static MilestoneActivitySnapshot From(ProjectManagementMilestoneEntity entity) => new(
            entity.MilestoneName, entity.Description, entity.OwnerUserId, entity.Status, entity.StartDate, entity.DueDate,
            entity.ProgressPercent, entity.SortOrder, entity.IsDeleted);
    }
    private async Task<ProjectManagementMilestoneResponse> MapAsync(ProjectManagementMilestoneEntity entity, CancellationToken cancellationToken)
    {
        var tasks = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(task => task.ProjectId == entity.ProjectId && task.MilestoneId == entity.Id && !task.IsDeleted)
            .ToListAsync(cancellationToken);
        return Map(entity, tasks);
    }

    private static ProjectManagementMilestoneResponse Map(ProjectManagementMilestoneEntity entity, IReadOnlyList<ProjectManagementTaskEntity>? tasks = null)
    {
        var taskList = tasks ?? [];
        var leaves = taskList.Where(task => !taskList.Any(child => child.ParentTaskId == task.Id)).ToList();
        var weight = leaves.Sum(task => task.Weight);
        var progress = weight > 0 ? Math.Round(leaves.Sum(task => task.ProgressPercent * task.Weight) / weight, 2) : entity.ProgressPercent;
        var health = entity.Status == ProjectManagementDomainRules.MilestoneCompleted || progress >= 100 ? "Done" :
            entity.DueDate.HasValue && entity.DueDate.Value.Date < DateTime.UtcNow.Date ? "OffTrack" :
            entity.DueDate.HasValue && entity.DueDate.Value.Date <= DateTime.UtcNow.Date.AddDays(7) && progress < 80 ? "AtRisk" : "OnTrack";
        return new(entity.Id, entity.ProjectId, entity.MilestoneName, entity.Description, entity.OwnerUserId, entity.Status, health, entity.StartDate, entity.DueDate, entity.CompletedAt, progress, leaves.Count, leaves.Count(task => task.Status == ProjectManagementDomainRules.TaskDone), entity.SortOrder, entity.VersionNo);
    }
}
