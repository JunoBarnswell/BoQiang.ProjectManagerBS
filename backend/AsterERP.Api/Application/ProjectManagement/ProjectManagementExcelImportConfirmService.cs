using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using SqlSugar;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementExcelImportConfirmService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementExcelImportService previewService,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementTaskProgressProjector progressProjector,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementNotificationService? notificationService = null,
    IProjectManagementSearchService? searchService = null) : IProjectManagementExcelImportConfirmService, ITransientDependency
{
    private const string ImportOperationType = "project.excel-import";
    private const int MaxIdempotencyKeyLength = 160;

    public async Task<ProjectManagementExcelImportResultResponse> ConfirmAsync(
        ProjectManagementExcelImportConfirmRequest request,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var idempotencyKey = NormalizeIdempotencyKey(request.IdempotencyKey);
        var replay = await FindByIdempotencyKeyAsync(idempotencyKey, cancellationToken);
        if (replay is not null)
            return replay with { Status = ProjectManagementExcelImportResultStatuses.Replayed, Replayed = true };

        var snapshot = await previewService.CreateSnapshotAsync(file, cancellationToken);
        if (!string.Equals(snapshot.Preview.PreviewId, request.PreviewId?.Trim(), StringComparison.Ordinal))
            throw new ValidationException("预览快照已过期或上传文件内容已变化，请重新预览");

        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        if (!string.Equals(snapshot.Preview.Status, ProjectManagementExcelImportPreviewStatuses.Completed, StringComparison.Ordinal))
        {
            var rejected = BuildRejectedResult(snapshot, idempotencyKey, traceId);
            return await PersistFailureAsync(rejected, cancellationToken);
        }

        var rows = snapshot.Rows;
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var userId = RequireUser();
        var now = DateTime.UtcNow;
        var importId = Guid.NewGuid().ToString("N");
        var rowResults = new List<ProjectManagementExcelImportRowResult>(rows.Count);
        var impactedProjectIds = rows
            .Select(row => Value(row, "ProjectId") ?? row.StableId)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

        await EnsurePermissionsAsync(rows, cancellationToken);
        var db = databaseAccessor.GetProjectManagementDb();
        var projectsById = await LoadProjectsAsync(db, rows, tenantId, appCode, cancellationToken);
        var tasksById = await LoadTasksAsync(db, rows, tenantId, appCode, cancellationToken);
        var membersById = await LoadMembersAsync(db, rows, tenantId, appCode, cancellationToken);
        var projectEntities = new Dictionary<string, ProjectManagementProjectEntity>(projectsById, StringComparer.Ordinal);
        var taskEntities = new Dictionary<string, ProjectManagementTaskEntity>(tasksById, StringComparer.Ordinal);
        var projectIdsForNotification = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = RequiredStableId(row);
                    if (!projectEntities.TryGetValue(id, out var entity))
                    {
                        EnsureNewVersion(row);
                        entity = NewProject(row, id, tenantId, appCode, userId, now);
                        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                        projectEntities[id] = entity;
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Added, null, entity.VersionNo));
                    }
                    else
                    {
                        EnsureVersion(row, entity.VersionNo);
                        var expectedVersion = entity.VersionNo;
                        ApplyProject(entity, row, userId, now);
                        entity.VersionNo = expectedVersion + 1;
                        var affected = await db.Updateable(entity)
                            .Where(item => item.Id == id && item.VersionNo == expectedVersion && !item.IsDeleted)
                            .ExecuteCommandAsync(cancellationToken);
                        if (affected != 1) throw new ImportConflictException(row, "项目在确认导入期间已被修改");
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Updated, null, entity.VersionNo));
                    }
                    projectIdsForNotification.Add(id);
                    await WriteJournalAsync(entity, "excel-import", traceId, cancellationToken);
                    await WriteActivityAsync(entity.Id, "project.excel-imported", $"Excel 导入项目 {entity.ProjectName}", entity.Id, traceId, cancellationToken);
                }

                foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = RequiredStableId(row);
                    var projectId = RequiredValue(row, "ProjectId");
                    if (!projectEntities.ContainsKey(projectId)) throw new ImportConflictException(row, "引用项目在确认导入期间不存在");
                    if (!taskEntities.TryGetValue(id, out var entity))
                    {
                        EnsureNewVersion(row);
                        entity = NewTask(row, id, projectId, tenantId, appCode, userId, now);
                        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                        taskEntities[id] = entity;
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Added, null, entity.VersionNo));
                    }
                    else
                    {
                        EnsureVersion(row, entity.VersionNo);
                        var expectedVersion = entity.VersionNo;
                        ApplyTask(entity, row, projectId, userId, now);
                        entity.VersionNo = expectedVersion + 1;
                        var affected = await db.Updateable(entity)
                            .Where(item => item.Id == id && item.VersionNo == expectedVersion && !item.IsDeleted)
                            .ExecuteCommandAsync(cancellationToken);
                        if (affected != 1) throw new ImportConflictException(row, "任务在确认导入期间已被修改");
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Updated, null, entity.VersionNo));
                    }
                    impactedProjectIds.Add(projectId);
                    await WriteJournalAsync(entity, "excel-import", traceId, cancellationToken);
                    await WriteActivityAsync(entity.Id, "task.excel-imported", $"Excel 导入任务 {entity.Title}", projectId, traceId, cancellationToken);
                }

                foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.MembersSheet))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var id = RequiredStableId(row);
                    var projectId = RequiredValue(row, "ProjectId");
                    if (!projectEntities.ContainsKey(projectId)) throw new ImportConflictException(row, "引用项目在确认导入期间不存在");
                    if (!membersById.TryGetValue(id, out var entity))
                    {
                        EnsureNewVersion(row);
                        entity = NewMember(row, id, projectId, tenantId, appCode, userId, now);
                        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
                        membersById[id] = entity;
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Added, null, entity.VersionNo));
                    }
                    else
                    {
                        EnsureVersion(row, entity.VersionNo);
                        var expectedVersion = entity.VersionNo;
                        ApplyMember(entity, row, userId, now);
                        entity.VersionNo = expectedVersion + 1;
                        var affected = await db.Updateable(entity)
                            .Where(item => item.Id == id && item.VersionNo == expectedVersion && !item.IsDeleted)
                            .ExecuteCommandAsync(cancellationToken);
                        if (affected != 1) throw new ImportConflictException(row, "成员关系在确认导入期间已被修改");
                        rowResults.Add(Result(row, ProjectManagementExcelImportRowStatuses.Updated, null, entity.VersionNo));
                    }
                    impactedProjectIds.Add(projectId);
                    await WriteJournalAsync(entity, "excel-import", traceId, cancellationToken);
                }

                foreach (var projectId in impactedProjectIds)
                {
                    await progressProjector.RefreshAsync(projectId, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                var importedTaskIds = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet)
                    .Select(item => item.StableId).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).ToArray();
                if (importedTaskIds.Length > 0)
                {
                    await db.Deleteable<ProjectManagementTaskLabelEntity>().Where(item => importedTaskIds.Contains(item.TaskId)).ExecuteCommandAsync(cancellationToken);
                    var labels = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet)
                        .SelectMany(row => Tokens(Value(row, "Labels")).Select(labelId => (row, labelId)))
                        .Select(item => new ProjectManagementTaskLabelEntity
                        {
                            Id = Guid.NewGuid().ToString("N"), TenantId = tenantId, AppCode = appCode,
                            ProjectId = RequiredValue(item.row, "ProjectId"), TaskId = RequiredStableId(item.row), LabelId = item.labelId,
                            VersionNo = 1, CreatedBy = userId, CreatedTime = now
                        }).ToList();
                    if (labels.Count > 0) await db.Insertable(labels).ExecuteCommandAsync(cancellationToken);

                    await db.Deleteable<ProjectManagementTaskDependencyEntity>().Where(item => importedTaskIds.Contains(item.SuccessorTaskId) && !item.IsDeleted).ExecuteCommandAsync(cancellationToken);
                    var dependencies = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet)
                        .SelectMany(row => Tokens(Value(row, "DependencyIds")).Select(predecessor => (row, predecessor)))
                        .Select(item => new ProjectManagementTaskDependencyEntity
                        {
                            Id = Guid.NewGuid().ToString("N"), TenantId = tenantId, AppCode = appCode,
                            ProjectId = RequiredValue(item.row, "ProjectId"), PredecessorTaskId = item.predecessor,
                            SuccessorTaskId = RequiredStableId(item.row), DependencyType = "FinishToStart", LagMinutes = 0,
                            VersionNo = 1, CreatedBy = userId, CreatedTime = now
                        }).ToList();
                    if (dependencies.Count > 0) await db.Insertable(dependencies).ExecuteCommandAsync(cancellationToken);
                }

                var completedAt = DateTime.UtcNow;
                var result = BuildResult(importId, snapshot.Preview.PreviewId, idempotencyKey, ProjectManagementExcelImportResultStatuses.Succeeded, traceId, completedAt, rowResults, false);
                await db.Insertable(new ProjectManagementOperationEntity
                {
                    Id = importId, TenantId = tenantId, AppCode = appCode, OperationType = ImportOperationType,
                    Status = "Succeeded", Phase = "Completed", ProgressPercent = 100, VersionNo = 1,
                    ImpactJson = JsonSerializer.Serialize(new ImportImpact(idempotencyKey, result)), TraceId = traceId,
                    ActorUserId = userId, StartedTime = now, CompletedTime = completedAt,
                    CreatedBy = userId, CreatedTime = now
                }).ExecuteCommandAsync(cancellationToken);
            });
        }
        catch (ImportConflictException conflict)
        {
            var conflictRows = rows.Select(row => row.StableId == conflict.Row.StableId && row.SheetName == conflict.Row.SheetName
                ? Result(row, ProjectManagementExcelImportRowStatuses.Conflict, conflict.Message, null)
                : Result(row, ProjectManagementExcelImportRowStatuses.Skipped, "事务已回滚，未写入", null)).ToList();
            return await PersistFailureAsync(BuildResult(importId, snapshot.Preview.PreviewId, idempotencyKey, ProjectManagementExcelImportResultStatuses.Failed, traceId, DateTime.UtcNow, conflictRows, false), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var failedRows = rows.Select(row => Result(row, ProjectManagementExcelImportRowStatuses.Failed, "事务已回滚，未写入", null)).ToList();
            return await PersistFailureAsync(BuildResult(importId, snapshot.Preview.PreviewId, idempotencyKey, ProjectManagementExcelImportResultStatuses.Failed, traceId, DateTime.UtcNow, failedRows, false), cancellationToken);
        }

        foreach (var projectId in projectIdsForNotification)
        {
            try
            {
                if (notificationService is not null && projectEntities.TryGetValue(projectId, out var project))
                    await notificationService.PublishAsync(new ProjectManagementNotification(tenantId, appCode, "project.excel-import", project.OwnerUserId, "Excel 导入完成", $"项目 {project.ProjectName} 已完成 Excel 导入", $"/projects/{project.Id}/tasks", traceId, project.Id), CancellationToken.None);
            }
            catch { }
        }
        try { if (searchService is not null) await searchService.QueueIndexIncrementalAsync(new ProjectManagementSearchIndexOperationRequest(), CancellationToken.None); } catch { }
        return BuildResult(importId, snapshot.Preview.PreviewId, idempotencyKey, ProjectManagementExcelImportResultStatuses.Succeeded, traceId, DateTime.UtcNow, rowResults, false);
    }

    public async Task<ProjectManagementExcelImportResultResponse> GetResultAsync(string importId, CancellationToken cancellationToken = default)
    {
        RequireWorkspace();
        var operation = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == importId && item.OperationType == ImportOperationType && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
            ?? throw new ValidationException("Excel 导入结果不存在");
        var impact = JsonSerializer.Deserialize<ImportImpact>(operation.ImpactJson)
            ?? throw new ValidationException("Excel 导入结果已损坏");
        return impact.Result;
    }

    private async Task<ProjectManagementExcelImportResultResponse?> FindByIdempotencyKeyAsync(string key, CancellationToken cancellationToken)
    {
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.OperationType == ImportOperationType && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(100).ToListAsync(cancellationToken);
        foreach (var row in rows)
        {
            var impact = JsonSerializer.Deserialize<ImportImpact>(row.ImpactJson);
            if (impact is not null && string.Equals(impact.IdempotencyKey, key, StringComparison.Ordinal)) return impact.Result;
        }
        return null;
    }

    private async Task<ProjectManagementExcelImportResultResponse> PersistFailureAsync(ProjectManagementExcelImportResultResponse result, CancellationToken cancellationToken)
    {
        var replay = await FindByIdempotencyKeyAsync(result.IdempotencyKey, cancellationToken);
        if (replay is not null) return replay with { Status = ProjectManagementExcelImportResultStatuses.Replayed, Replayed = true };
        var tenant = RequireTenant(); var app = RequireApp(); var user = RequireUser();
        await databaseAccessor.GetProjectManagementDb().Insertable(new ProjectManagementOperationEntity
        {
            Id = result.ImportId, TenantId = tenant, AppCode = app, OperationType = ImportOperationType,
            Status = "Failed", Phase = "Failed", ProgressPercent = 0, VersionNo = 1,
            ImpactJson = JsonSerializer.Serialize(new ImportImpact(result.IdempotencyKey, result)), TraceId = result.TraceId,
            ActorUserId = user, StartedTime = result.CompletedAt, CompletedTime = result.CompletedAt,
            CreatedBy = user, CreatedTime = result.CompletedAt
        }).ExecuteCommandAsync(cancellationToken);
        return result;
    }

    private async Task EnsurePermissionsAsync(IReadOnlyList<ProjectManagementExcelImportSnapshotRow> rows, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetProjectManagementDb();
        var existingProjectIds = rows
            .Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet)
            .Select(item => item.StableId)
            .Concat(rows
                .Where(item => item.SheetName is ProjectManagementExcelImportTemplate.TasksSheet or ProjectManagementExcelImportTemplate.MembersSheet)
                .Select(item => Value(item, "ProjectId")))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var existing = existingProjectIds.Length == 0 ? [] : await db.Queryable<ProjectManagementProjectEntity>().Where(item => existingProjectIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
        var existingIds = existing.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet))
        {
            var id = RequiredStableId(row);
            if (existingIds.Contains(id)) await accessPolicy.EnsureCanManageProjectAsync(id, cancellationToken);
            else if (!CanBypassObjectPermission() && !string.Equals(Value(row, "OwnerUserId") ?? RequireUser(), RequireUser(), StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("新项目负责人必须是当前用户", AsterERP.Shared.ErrorCodes.PermissionDenied);
        }
        foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet))
        {
            var projectId = RequiredValue(row, "ProjectId");
            if (!existingIds.Contains(projectId)) continue;
            await accessPolicy.EnsureCanManageTaskAsync(projectId, row.StableId, Value(row, "ParentTaskId"), Value(row, "AssigneeUserId"), cancellationToken: cancellationToken);
        }
        foreach (var row in rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.MembersSheet))
        {
            var projectId = RequiredValue(row, "ProjectId");
            if (existingIds.Contains(projectId)) await accessPolicy.EnsureCanManageMembersAsync(projectId, cancellationToken);
        }
    }

    private async Task<Dictionary<string, ProjectManagementProjectEntity>> LoadProjectsAsync(ISqlSugarClient db, IReadOnlyList<ProjectManagementExcelImportSnapshotRow> rows, string tenant, string app, CancellationToken cancellationToken)
    {
        var ids = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.ProjectsSheet).Select(item => item.StableId).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct().ToArray();
        var items = ids.Length == 0 ? [] : await db.Queryable<ProjectManagementProjectEntity>().Where(item => ids.Contains(item.Id) && item.TenantId == tenant && item.AppCode == app && !item.IsDeleted).ToListAsync(cancellationToken);
        return items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, ProjectManagementTaskEntity>> LoadTasksAsync(ISqlSugarClient db, IReadOnlyList<ProjectManagementExcelImportSnapshotRow> rows, string tenant, string app, CancellationToken cancellationToken)
    {
        var ids = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.TasksSheet).Select(item => item.StableId).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct().ToArray();
        var items = ids.Length == 0 ? [] : await db.Queryable<ProjectManagementTaskEntity>().Where(item => ids.Contains(item.Id) && item.TenantId == tenant && item.AppCode == app && !item.IsDeleted).ToListAsync(cancellationToken);
        return items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private async Task<Dictionary<string, ProjectManagementProjectMemberEntity>> LoadMembersAsync(ISqlSugarClient db, IReadOnlyList<ProjectManagementExcelImportSnapshotRow> rows, string tenant, string app, CancellationToken cancellationToken)
    {
        var ids = rows.Where(item => item.SheetName == ProjectManagementExcelImportTemplate.MembersSheet).Select(item => item.StableId).Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item!).Distinct().ToArray();
        var items = ids.Length == 0 ? [] : await db.Queryable<ProjectManagementProjectMemberEntity>().Where(item => ids.Contains(item.Id) && item.TenantId == tenant && item.AppCode == app && !item.IsDeleted).ToListAsync(cancellationToken);
        return items.ToDictionary(item => item.Id, StringComparer.Ordinal);
    }

    private static ProjectManagementProjectEntity NewProject(ProjectManagementExcelImportSnapshotRow row, string id, string tenant, string app, string user, DateTime now)
    {
        var entity = new ProjectManagementProjectEntity { Id = id, TenantId = tenant, AppCode = app, VersionNo = 1, CreatedBy = user, CreatedTime = now };
        ApplyProject(entity, row, user, now); return entity;
    }

    private static void ApplyProject(ProjectManagementProjectEntity entity, ProjectManagementExcelImportSnapshotRow row, string user, DateTime now)
    {
        entity.ProjectCode = RequiredValue(row, "ProjectCode"); entity.ProjectName = RequiredValue(row, "ProjectName"); entity.Description = Optional(Value(row, "Description"));
        entity.Status = RequiredValue(row, "Status"); entity.Priority = RequiredValue(row, "Priority"); entity.OwnerUserId = Optional(Value(row, "OwnerUserId")) ?? user;
        entity.StartDate = Date(Value(row, "StartDate")); entity.DueDate = Date(Value(row, "DueDate")); entity.WipLimit = Int(Value(row, "WipLimit")); entity.ProgressPercent = Decimal(Value(row, "ProgressPercent"));
        entity.CompletedAt = entity.Status == ProjectManagementDomainRules.ProjectCompleted ? entity.CompletedAt ?? now : null; entity.UpdatedBy = user; entity.UpdatedTime = now;
    }

    private static ProjectManagementTaskEntity NewTask(ProjectManagementExcelImportSnapshotRow row, string id, string projectId, string tenant, string app, string user, DateTime now)
    {
        var entity = new ProjectManagementTaskEntity { Id = id, TenantId = tenant, AppCode = app, ProjectId = projectId, VersionNo = 1, CreatedBy = user, CreatedTime = now };
        ApplyTask(entity, row, projectId, user, now); return entity;
    }

    private static void ApplyTask(ProjectManagementTaskEntity entity, ProjectManagementExcelImportSnapshotRow row, string projectId, string user, DateTime now)
    {
        entity.ProjectId = projectId; entity.MilestoneId = Optional(Value(row, "MilestoneId")); entity.ParentTaskId = Optional(Value(row, "ParentTaskId")); entity.TaskCode = RequiredValue(row, "TaskCode"); entity.Title = RequiredValue(row, "Title");
        entity.Summary = Optional(Value(row, "Summary")); entity.Description = Optional(Value(row, "Description")); entity.Status = RequiredValue(row, "Status"); entity.Priority = RequiredValue(row, "Priority"); entity.AssigneeUserId = Optional(Value(row, "AssigneeUserId")); entity.AssigneeEmploymentId = Optional(Value(row, "AssigneeEmploymentId"));
        entity.StartDate = Date(Value(row, "StartDate")); entity.DueDate = Date(Value(row, "DueDate")); entity.ProgressPercent = Decimal(Value(row, "ProgressPercent")); entity.Weight = Decimal(Value(row, "Weight"), 1); entity.SortOrder = Int(Value(row, "SortOrder")) ?? 0; entity.Depth = 0; entity.UpdatedBy = user; entity.UpdatedTime = now;
    }

    private static ProjectManagementProjectMemberEntity NewMember(ProjectManagementExcelImportSnapshotRow row, string id, string projectId, string tenant, string app, string user, DateTime now)
    {
        var entity = new ProjectManagementProjectMemberEntity { Id = id, TenantId = tenant, AppCode = app, ProjectId = projectId, VersionNo = 1, CreatedBy = user, CreatedTime = now };
        ApplyMember(entity, row, user, now); return entity;
    }

    private static void ApplyMember(ProjectManagementProjectMemberEntity entity, ProjectManagementExcelImportSnapshotRow row, string user, DateTime now)
    {
        entity.ProjectId = RequiredValue(row, "ProjectId"); entity.UserId = RequiredValue(row, "UserId"); entity.EmploymentId = Optional(Value(row, "EmploymentId")); entity.RoleCode = RequiredValue(row, "RoleCode"); entity.ScopeRootTaskId = Optional(Value(row, "ScopeRootTaskId")); entity.IsActive = Bool(Value(row, "IsActive"), true); entity.JoinedAt = Date(Value(row, "JoinedAt")) ?? now; entity.LeftAt = Date(Value(row, "LeftAt")); entity.UpdatedBy = user; entity.UpdatedTime = now;
    }

    private async Task WriteJournalAsync(ProjectManagementProjectEntity entity, string operation, string traceId, CancellationToken cancellationToken) =>
        await (syncJournalWriter?.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenant(), RequireApp(), "Project", entity.Id, entity.Id, operation, entity.VersionNo, JsonSerializer.Serialize(entity), RequireUser(), null, traceId), cancellationToken) ?? Task.CompletedTask);

    private async Task WriteJournalAsync(ProjectManagementTaskEntity entity, string operation, string traceId, CancellationToken cancellationToken) =>
        await (syncJournalWriter?.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenant(), RequireApp(), "Task", entity.Id, entity.ProjectId, operation, entity.VersionNo, JsonSerializer.Serialize(entity), RequireUser(), null, traceId), cancellationToken) ?? Task.CompletedTask);

    private async Task WriteJournalAsync(ProjectManagementProjectMemberEntity entity, string operation, string traceId, CancellationToken cancellationToken) =>
        await (syncJournalWriter?.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenant(), RequireApp(), "ProjectMember", entity.Id, entity.ProjectId, operation, entity.VersionNo, JsonSerializer.Serialize(entity), RequireUser(), null, traceId), cancellationToken) ?? Task.CompletedTask);

    private async Task WriteActivityAsync(string aggregateId, string activityType, string summary, string projectId, string traceId, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenant(), RequireApp(), "ExcelImport", aggregateId, activityType, summary, traceId, RequireUser(), projectId), cancellationToken);
    }

    private ProjectManagementExcelImportResultResponse BuildRejectedResult(ProjectManagementExcelImportSnapshot snapshot, string key, string traceId)
    {
        var rows = snapshot.Rows.Select(row => Result(row, row.Issues.Any(issue => issue.Severity == "Error") ? ProjectManagementExcelImportRowStatuses.Failed : ProjectManagementExcelImportRowStatuses.Warning, row.Issues.FirstOrDefault()?.Message, null)).ToList();
        return BuildResult(Guid.NewGuid().ToString("N"), snapshot.Preview.PreviewId, key, ProjectManagementExcelImportResultStatuses.Failed, traceId, DateTime.UtcNow, rows, false);
    }

    private static ProjectManagementExcelImportResultResponse BuildResult(string importId, string previewId, string key, string status, string traceId, DateTime completedAt, IReadOnlyList<ProjectManagementExcelImportRowResult> rows, bool replayed) =>
        new(importId, previewId, key, status, traceId, completedAt,
            rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Added), rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Updated), rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Skipped), rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Failed), rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Conflict), rows.Count(item => item.Status == ProjectManagementExcelImportRowStatuses.Warning), rows, replayed);

    private static ProjectManagementExcelImportRowResult Result(ProjectManagementExcelImportSnapshotRow row, string status, string? message, long? version) => new(row.SheetName, row.RowNumber, row.StableId, status, message, version);
    private static string RequiredStableId(ProjectManagementExcelImportSnapshotRow row) => RequiredValue(row, "StableId");
    private static string RequiredValue(ProjectManagementExcelImportSnapshotRow row, string field) => Value(row, field)?.Trim() ?? throw new ValidationException($"{field} 不能为空");
    private static string? Value(ProjectManagementExcelImportSnapshotRow row, string field) => row.Values.TryGetValue(field, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static IEnumerable<string> Tokens(string? value) => (value ?? string.Empty).Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    private static DateTime? Date(string? value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var result) || DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out result) ? result : null;
    private static decimal Decimal(string? value, decimal fallback = 0) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) || decimal.TryParse(value, NumberStyles.Number, CultureInfo.CurrentCulture, out result) ? result : fallback;
    private static int? Int(string? value) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) || int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out result) ? result : null;
    private static bool Bool(string? value, bool fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value is "1" or "是" || bool.TryParse(value, out var result) && result;
    private static void EnsureNewVersion(ProjectManagementExcelImportSnapshotRow row) { if (long.TryParse(Value(row, "VersionNo"), out var version) && version != 1) throw new ImportConflictException(row, "新增对象 VersionNo 必须为 1"); }
    private static void EnsureVersion(ProjectManagementExcelImportSnapshotRow row, long actual) { if (!long.TryParse(Value(row, "VersionNo"), out var expected) || expected != actual) throw new ImportConflictException(row, $"版本冲突，当前版本为 {actual}"); }
    private static bool CanBypassObjectPermission() => false;
    private void RequireWorkspace() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string RequireUser() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string NormalizeIdempotencyKey(string? value) { var key = value?.Trim() ?? string.Empty; if (key.Length is < 8 or > MaxIdempotencyKeyLength) throw new ValidationException($"幂等键长度必须在 8 到 {MaxIdempotencyKeyLength} 个字符之间"); return key; }

    private sealed record ImportImpact(string IdempotencyKey, ProjectManagementExcelImportResultResponse Result);
    private sealed class ImportConflictException(ProjectManagementExcelImportSnapshotRow row, string message) : Exception(message) { public ProjectManagementExcelImportSnapshotRow Row { get; } = row; }
}
