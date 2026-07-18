using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Domain.Common;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementSearchService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    IProjectManagementOperationWriter? operationWriter = null,
    IBackgroundJobManager? backgroundJobManager = null) : IProjectManagementSearchService
{
    private const int MaxLimit = 50;
    private const int MaxBatchSize = 1000;
    private const string RebuildOperation = "search.index.rebuild";
    private const string IncrementalOperation = "search.index.incremental";
    private const string RecoveryOperation = "search.index.recover";
    private const string IndexTable = "pm_search_index";
    private const string StateTable = "pm_search_index_state";
    private static readonly SemaphoreSlim IndexLock = new(1, 1);

    public async Task<ProjectManagementSearchResponse> SearchAsync(
        ProjectManagementSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var keyword = Required(query.Keyword, "搜索关键字不能为空");
        if (keyword.Length > 200) throw new ValidationException("搜索关键字不能超过 200 个字符");
        var scope = NormalizeScope(query.Scope);
        var limit = Math.Clamp(query.Limit, 1, MaxLimit);
        var pageIndex = Math.Max(query.PageIndex, 1);
        var projectId = NormalizeOptional(query.ProjectId);
        var status = NormalizeOptional(query.Status);
        if (status is not null && status.Length > 64) throw new ValidationException("状态筛选不能超过 64 个字符");
        if (query.From.HasValue && query.To.HasValue && query.From > query.To)
            throw new ValidationException("搜索时间范围无效");
        RequireTenant();
        RequireApp();

        var db = databaseAccessor.GetProjectManagementDb();
        var projects = new List<ProjectManagementSearchItem>();
        var tasks = new List<ProjectManagementSearchItem>();
        var milestones = new List<ProjectManagementSearchItem>();
        var labels = new List<ProjectManagementSearchItem>();
        var members = new List<ProjectManagementSearchItem>();
        var comments = new List<ProjectManagementSearchItem>();

        if (scope is "all" or "projects")
        {
            var projectQuery = db.Queryable<ProjectManagementProjectEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.ProjectCode.Contains(keyword) || item.ProjectName.Contains(keyword) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) projectQuery = projectQuery.Where(item => item.Id == projectId);
            if (status is not null) projectQuery = projectQuery.Where(item => item.Status == status);
            projectQuery = ApplyTime(projectQuery, query.From, query.To);
            var rows = await projectQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            projects = rows.Select(item => new ProjectManagementSearchItem(
                "project", item.Id, item.Id, item.ProjectName,
                Snippet(item.Description ?? item.ProjectCode, keyword),
                $"/projects/{Segment(item.Id)}/overview", item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "tasks")
        {
            var taskQuery = db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.TaskCode.Contains(keyword) || item.Title.Contains(keyword) ||
                     (item.Summary != null && item.Summary.Contains(keyword)) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) taskQuery = taskQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) taskQuery = taskQuery.Where(item => item.Status == status);
            taskQuery = ApplyTime(taskQuery, query.From, query.To);
            var rows = await taskQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            tasks = rows.Select(item => new ProjectManagementSearchItem(
                "task", item.Id, item.ProjectId, item.Title,
                Snippet(item.Description ?? item.Summary ?? item.TaskCode, keyword),
                $"/projects/{Segment(item.ProjectId)}/tasks?taskId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "milestones")
        {
            var milestoneQuery = db.Queryable<ProjectManagementMilestoneEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.MilestoneName.Contains(keyword) ||
                     (item.Description != null && item.Description.Contains(keyword))));
            if (projectId is not null) milestoneQuery = milestoneQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) milestoneQuery = milestoneQuery.Where(item => item.Status == status);
            milestoneQuery = ApplyTime(milestoneQuery, query.From, query.To);
            var rows = await milestoneQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            milestones = rows.Select(item => new ProjectManagementSearchItem(
                "milestone", item.Id, item.ProjectId, item.MilestoneName,
                Snippet(item.Description ?? item.Status, keyword),
                $"/projects/{Segment(item.ProjectId)}/milestones?milestoneId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "labels")
        {
            var labelQuery = db.Queryable<ProjectManagementLabelEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.LabelName.Contains(keyword) || item.Color.Contains(keyword)));
            if (projectId is not null) labelQuery = labelQuery.Where(item => item.ProjectId == null || item.ProjectId == projectId);
            labelQuery = ApplyTime(labelQuery, query.From, query.To);
            var rows = await labelQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            labels = rows.Select(item => new ProjectManagementSearchItem(
                "label", item.Id, item.ProjectId ?? string.Empty, item.LabelName,
                Snippet(item.Color, keyword),
                item.ProjectId is null
                    ? "/projects"
                    : $"/projects/{Segment(item.ProjectId)}/tasks?labelId={QueryValue(item.Id)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "members")
        {
            var memberQuery = db.Queryable<ProjectManagementProjectMemberEntity>()
                .Where(item => !item.IsDeleted && item.IsActive &&
                    (item.UserId.Contains(keyword) ||
                     (item.EmploymentId != null && item.EmploymentId.Contains(keyword)) ||
                     item.RoleCode.Contains(keyword)));
            if (projectId is not null) memberQuery = memberQuery.Where(item => item.ProjectId == projectId);
            if (status is not null) memberQuery = memberQuery.Where(item => item.RoleCode == status);
            memberQuery = ApplyTime(memberQuery, query.From, query.To);
            var rows = await memberQuery
                .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            members = rows.Select(item => new ProjectManagementSearchItem(
                "member", item.Id, item.ProjectId, item.UserId,
                Snippet(item.EmploymentId ?? item.RoleCode, keyword),
                $"/projects/{Segment(item.ProjectId)}/members?userId={QueryValue(item.UserId)}",
                item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        if (scope is "all" or "comments")
        {
            var commentQuery = db.Queryable<ProjectManagementTaskCommentEntity>()
                .Where(item => !item.IsDeleted &&
                    (item.Markdown.Contains(keyword) || item.AuthorUserId.Contains(keyword)));
            if (projectId is not null) commentQuery = commentQuery.Where(item => item.ProjectId == projectId);
            if (status is not null)
            {
                commentQuery = commentQuery.Where(item => SqlFunc.Subqueryable<ProjectManagementTaskEntity>()
                    .Where(task => task.Id == item.TaskId && task.Status == status && !task.IsDeleted)
                    .Any());
            }
            if (query.From.HasValue && query.To.HasValue)
            {
                var from = query.From.Value;
                var to = query.To.Value;
                commentQuery = commentQuery.Where(item =>
                    (item.CreatedTime >= from && item.CreatedTime <= to) ||
                    (item.EditedTime.HasValue && item.EditedTime.Value >= from && item.EditedTime.Value <= to));
            }
            else if (query.From.HasValue)
            {
                var from = query.From.Value;
                commentQuery = commentQuery.Where(item => item.CreatedTime >= from || (item.EditedTime.HasValue && item.EditedTime.Value >= from));
            }
            else if (query.To.HasValue)
            {
                var to = query.To.Value;
                commentQuery = commentQuery.Where(item => item.CreatedTime <= to || (item.EditedTime.HasValue && item.EditedTime.Value <= to));
            }
            var rows = await commentQuery
                .OrderBy(item => item.EditedTime, OrderByType.Desc)
                .OrderBy(item => item.CreatedTime, OrderByType.Desc)
                .OrderBy(item => item.Id, OrderByType.Desc)
                .ToPageListAsync(pageIndex, limit, new RefAsync<int>(), cancellationToken);
            comments = rows.Select(item => new ProjectManagementSearchItem(
                "comment", item.Id, item.ProjectId, "任务评论",
                Snippet(item.Markdown, keyword),
                $"/projects/{Segment(item.ProjectId)}/tasks?taskId={QueryValue(item.TaskId)}&commentId={QueryValue(item.Id)}",
                item.EditedTime ?? item.UpdatedTime ?? item.CreatedTime)).ToList();
        }

        return new ProjectManagementSearchResponse(projects, tasks, milestones, labels, members, comments);
    }

    public async Task<ProjectManagementSearchIndexStatusResponse> GetIndexStatusAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var db = databaseAccessor.GetProjectManagementDb();
        await EnsureIndexStorageAsync(db, cancellationToken);
        return await ReadStatusAsync(db, tenantId, appCode, cancellationToken);
    }

    public Task<ProjectManagementSearchIndexOperationResponse> QueueIndexRebuildAsync(
        ProjectManagementSearchIndexOperationRequest request,
        CancellationToken cancellationToken = default) =>
        QueueIndexOperationAsync(RebuildOperation, request, cancellationToken);

    public Task<ProjectManagementSearchIndexOperationResponse> QueueIndexIncrementalAsync(
        ProjectManagementSearchIndexOperationRequest request,
        CancellationToken cancellationToken = default) =>
        QueueIndexOperationAsync(IncrementalOperation, request, cancellationToken);

    public Task<ProjectManagementSearchIndexOperationResponse> QueueIndexRecoveryAsync(
        ProjectManagementSearchIndexOperationRequest request,
        CancellationToken cancellationToken = default) =>
        QueueIndexOperationAsync(RecoveryOperation, request, cancellationToken);

    public async Task<bool> TryExecuteIndexOperationAsync(
        ProjectManagementOperationJobArgs args,
        CancellationToken cancellationToken = default)
    {
        var operation = (await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == args.OperationId && item.TenantId == args.TenantId && item.AppCode == args.AppCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (operation?.OperationType is not (RebuildOperation or IncrementalOperation or RecoveryOperation)) return false;

        var impact = DeserializeImpact(operation.ImpactJson);
        var writer = operationWriter;
        try
        {
            if (writer is not null) await writer.StartAsync(operation.Id, operation.OperationType, operation.ImpactJson, operation.TraceId, cancellationToken);
            var status = operation.OperationType == RebuildOperation
                ? await RebuildIndexCoreAsync(operation.Id, impact.BatchSize, cancellationToken)
                : operation.OperationType == IncrementalOperation
                    ? await IncrementalIndexCoreAsync(operation.Id, impact.BatchSize, cancellationToken)
                    : await RecoverIndexCoreAsync(operation.Id, impact.BatchSize, cancellationToken);
            if (writer is not null) await writer.CompleteWithImpactAsync(operation.Id, JsonSerializer.Serialize(status), cancellationToken);
        }
        catch (Exception exception)
        {
            await MarkFailedAsync(args.TenantId, args.AppCode, operation.Id, exception, CancellationToken.None);
            if (writer is not null) await writer.FailAsync(operation.Id, $"搜索索引作业失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return true;
    }

    private async Task<ProjectManagementSearchIndexOperationResponse> QueueIndexOperationAsync(
        string operationType,
        ProjectManagementSearchIndexOperationRequest request,
        CancellationToken cancellationToken)
    {
        var writer = operationWriter ?? throw new InvalidOperationException("搜索索引长任务写入器未注册");
        var jobManager = backgroundJobManager ?? throw new InvalidOperationException("搜索索引后台队列未注册");
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var userId = RequireUser();
        var batchSize = Math.Clamp(request.BatchSize, 1, MaxBatchSize);
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = Activity.Current?.Id ?? operationId;
        var impactJson = JsonSerializer.Serialize(new IndexImpact(batchSize));
        await writer.CreatePendingAsync(operationId, operationType, impactJson, traceId, cancellationToken);
        try
        {
            await jobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, tenantId, appCode, userId, traceId));
        }
        catch (Exception exception)
        {
            await writer.FailAsync(operationId, $"搜索索引入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return new ProjectManagementSearchIndexOperationResponse(operationId, operationType);
    }

    private async Task<ProjectManagementSearchIndexStatusResponse> RebuildIndexCoreAsync(
        string operationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await IndexLock.WaitAsync(cancellationToken);
        try
        {
            var tenantId = RequireTenant();
            var appCode = RequireApp();
            var db = databaseAccessor.GetProjectManagementDb();
            await EnsureIndexStorageAsync(db, cancellationToken);
            var current = await ReadStatusAsync(db, tenantId, appCode, cancellationToken);
            var target = await GetJournalWatermarkAsync(db, tenantId, appCode, cancellationToken);
            await WriteStateAsync(db, new IndexState(
                tenantId, appCode, "rebuild", "Rebuilding", 0, target, current.FailureCount,
                null, operationId, DateTime.UtcNow, null, DateTime.UtcNow), cancellationToken);

            var documents = await LoadAllDocumentsAsync(db, tenantId, appCode, cancellationToken);
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                await ExecuteNonQueryAsync(db, $"DELETE FROM {IndexTable} WHERE TenantId = @tenant AND AppCode = @app", cancellationToken,
                    ("@tenant", tenantId), ("@app", appCode));
                foreach (var document in documents.Chunk(Math.Max(1, batchSize)))
                {
                    foreach (var item in document) await UpsertDocumentAsync(db, tenantId, appCode, item, cancellationToken);
                }
            });

            await WriteStateAsync(db, new IndexState(
                tenantId, appCode, "rebuild", "Incremental", target, target, 0,
                null, operationId, current.StartedTime ?? DateTime.UtcNow, null, DateTime.UtcNow), cancellationToken);
            return await IncrementalIndexCoreWithoutLockAsync(operationId, batchSize, cancellationToken);
        }
        finally
        {
            IndexLock.Release();
        }
    }

    private async Task<ProjectManagementSearchIndexStatusResponse> IncrementalIndexCoreAsync(
        string operationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await IndexLock.WaitAsync(cancellationToken);
        try
        {
            return await IncrementalIndexCoreWithoutLockAsync(operationId, batchSize, cancellationToken);
        }
        finally
        {
            IndexLock.Release();
        }
    }

    private async Task<ProjectManagementSearchIndexStatusResponse> IncrementalIndexCoreWithoutLockAsync(
        string operationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var db = databaseAccessor.GetProjectManagementDb();
        await EnsureIndexStorageAsync(db, cancellationToken);
        var state = await ReadStatusAsync(db, tenantId, appCode, cancellationToken);
        var target = await GetJournalWatermarkAsync(db, tenantId, appCode, cancellationToken);
        if (state.Status == "Rebuilding") throw new InvalidOperationException("搜索索引正在重建，不能并行执行增量更新");
        await WriteStateAsync(db, new IndexState(
            tenantId, appCode, state.Mode == "rebuild" ? "rebuild" : "incremental", "Incremental",
            state.AppliedSequenceNo, target, state.FailureCount, null, operationId,
            state.StartedTime ?? DateTime.UtcNow, null, DateTime.UtcNow), cancellationToken);

        var applied = state.AppliedSequenceNo;
        while (applied < target)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var journal = await ReadJournalBatchAsync(db, tenantId, appCode, applied, target, batchSize, cancellationToken);
            if (journal.Count == 0) break;
            await ProjectManagementMutationTransaction.RunAsync(db, async () =>
            {
                foreach (var entry in journal) await ApplyJournalEntryAsync(db, tenantId, appCode, entry, cancellationToken);
            });
            applied = journal[^1].SequenceNo;
            await WriteStateAsync(db, new IndexState(
                tenantId, appCode, state.Mode == "rebuild" ? "rebuild" : "incremental", "Incremental",
                applied, target, 0, null, operationId, state.StartedTime ?? DateTime.UtcNow, null, DateTime.UtcNow), cancellationToken);
            if (operationWriter is not null)
            {
                var percent = target == 0 ? 100 : (int)Math.Min(99, applied * 100 / target);
                if (!await operationWriter.ReportProgressAsync(operationId, "正在应用同步 Journal", percent, cancellationToken)) break;
            }
        }

        var finalTarget = await GetJournalWatermarkAsync(db, tenantId, appCode, cancellationToken);
        var final = new IndexState(
            tenantId, appCode, state.Mode == "rebuild" ? "rebuild" : "incremental", applied >= finalTarget ? "Ready" : "Incremental",
            applied, finalTarget, 0, null, operationId, state.StartedTime ?? DateTime.UtcNow,
            applied >= finalTarget ? DateTime.UtcNow : null, DateTime.UtcNow);
        await WriteStateAsync(db, final, cancellationToken);
        return await ReadStatusAsync(db, tenantId, appCode, cancellationToken);
    }

    private async Task<ProjectManagementSearchIndexStatusResponse> RecoverIndexCoreAsync(
        string operationId,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var status = await GetIndexStatusAsync(cancellationToken);
        return status.Mode == "rebuild"
            ? await RebuildIndexCoreAsync(operationId, batchSize, cancellationToken)
            : await IncrementalIndexCoreAsync(operationId, batchSize, cancellationToken);
    }

    private async Task MarkFailedAsync(string tenantId, string appCode, string operationId, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            var db = databaseAccessor.GetProjectManagementDb();
            await EnsureIndexStorageAsync(db, cancellationToken);
            var current = await ReadStatusAsync(db, tenantId, appCode, cancellationToken);
            await WriteStateAsync(db, new IndexState(
                tenantId, appCode, current.Mode, "Failed", current.AppliedSequenceNo, current.TargetSequenceNo,
                current.FailureCount + 1, exception.Message.Length > 2000 ? exception.Message[..2000] : exception.Message,
                operationId, current.StartedTime, null, DateTime.UtcNow), cancellationToken);
        }
        catch
        {
            // Preserve the original operation failure; the operation record remains the recovery source.
        }
    }

    private async Task EnsureIndexStorageAsync(ISqlSugarClient db, CancellationToken cancellationToken)
    {
        var schema = new SqliteSchemaExecutor(db);
        await schema.ExecuteNonQueryAsync($"""
CREATE VIRTUAL TABLE IF NOT EXISTS {IndexTable} USING fts5(
    DocumentKey UNINDEXED,
    TenantId UNINDEXED,
    AppCode UNINDEXED,
    ResultType UNINDEXED,
    AggregateId UNINDEXED,
    ProjectId UNINDEXED,
    VersionNo UNINDEXED,
    SearchText,
    UpdatedTime UNINDEXED,
    tokenize = 'unicode61 remove_diacritics 2'
);
CREATE TABLE IF NOT EXISTS {StateTable} (
    TenantId TEXT NOT NULL,
    AppCode TEXT NOT NULL,
    Mode TEXT NOT NULL,
    Status TEXT NOT NULL,
    AppliedSequenceNo INTEGER NOT NULL,
    TargetSequenceNo INTEGER NOT NULL,
    FailureCount INTEGER NOT NULL DEFAULT 0,
    LastError TEXT NULL,
    OperationId TEXT NULL,
    StartedTime TEXT NULL,
    CompletedTime TEXT NULL,
    UpdatedTime TEXT NOT NULL,
    PRIMARY KEY (TenantId, AppCode)
);
""", cancellationToken);
    }

    private async Task<IndexedIds?> TryReadIndexedIdsAsync(
        ISqlSugarClient db,
        string keyword,
        string scope,
        int pageIndex,
        int limit,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureIndexStorageAsync(db, cancellationToken);
            var state = await ReadStatusAsync(db, RequireTenant(), RequireApp(), cancellationToken);
            var watermark = await GetJournalWatermarkAsync(db, RequireTenant(), RequireApp(), cancellationToken);
            if (state.Status != "Ready" || state.AppliedSequenceNo < watermark) return null;
            var match = BuildFtsQuery(keyword);
            if (match is null) return null;
            var maxCandidates = Math.Min(5000, Math.Max(limit, limit * Math.Max(pageIndex, 1) * 4));
            var rows = await ReadIndexHitsAsync(db, RequireTenant(), RequireApp(), match, maxCandidates, cancellationToken);
            var selected = scope switch
            {
                "projects" => rows.Where(item => item.ResultType == "project"),
                "tasks" => rows.Where(item => item.ResultType == "task"),
                "milestones" => rows.Where(item => item.ResultType == "milestone"),
                "labels" => rows.Where(item => item.ResultType == "label"),
                "members" => rows.Where(item => item.ResultType == "member"),
                "comments" => rows.Where(item => item.ResultType == "comment"),
                _ => rows
            };
            return new IndexedIds(
                selected.Where(item => item.ResultType == "project").Select(item => item.AggregateId).Distinct().ToList(),
                selected.Where(item => item.ResultType == "task").Select(item => item.AggregateId).Distinct().ToList(),
                selected.Where(item => item.ResultType == "milestone").Select(item => item.AggregateId).Distinct().ToList(),
                selected.Where(item => item.ResultType == "label").Select(item => item.AggregateId).Distinct().ToList(),
                selected.Where(item => item.ResultType == "member").Select(item => item.AggregateId).Distinct().ToList(),
                selected.Where(item => item.ResultType == "comment").Select(item => item.AggregateId).Distinct().ToList());
        }
        catch
        {
            // Search remains available through the existing permission-filtered SQL path until the index recovers.
            return null;
        }
    }

    private async Task<IReadOnlyList<IndexDocument>> LoadAllDocumentsAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var documents = new List<IndexDocument>();
        foreach (var definition in DocumentDefinitions)
        {
            documents.AddRange(await ReadDocumentsAsync(db, definition, tenantId, appCode, null, cancellationToken));
        }
        return documents;
    }

    private async Task ApplyJournalEntryAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        JournalRow entry,
        CancellationToken cancellationToken)
    {
        var definition = DocumentDefinitions.FirstOrDefault(item => string.Equals(item.AggregateType, entry.AggregateType, StringComparison.OrdinalIgnoreCase));
        if (definition is null) return;
        var key = DocumentKey(tenantId, appCode, definition.ResultType, entry.AggregateId);
        var currentVersion = await ReadIndexedVersionAsync(db, key, cancellationToken);
        if (currentVersion.HasValue && currentVersion.Value >= entry.VersionNo) return;
        var document = await ReadDocumentsAsync(db, definition, tenantId, appCode, entry.AggregateId, cancellationToken);
        if (document.Count == 0)
        {
            await ExecuteNonQueryAsync(db, $"DELETE FROM {IndexTable} WHERE DocumentKey = @key", cancellationToken, ("@key", key));
            return;
        }
        await UpsertDocumentAsync(db, tenantId, appCode, document[0], cancellationToken);
    }

    private static readonly DocumentDefinition[] DocumentDefinitions =
    [
        new("Project", "project", """
            SELECT Id, Id AS ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(ProjectCode, '') || ' ' || COALESCE(ProjectName, '') || ' ' || COALESCE(Description, '') || ' ' || COALESCE(Status, '')) AS SearchText
            FROM pm_projects
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0
        """),
        new("Task", "task", """
            SELECT Id, ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(TaskCode, '') || ' ' || COALESCE(Title, '') || ' ' || COALESCE(Summary, '') || ' ' || COALESCE(Description, '') || ' ' || COALESCE(Status, '')) AS SearchText
            FROM pm_tasks
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0
        """),
        new("Milestone", "milestone", """
            SELECT Id, ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(MilestoneName, '') || ' ' || COALESCE(Description, '') || ' ' || COALESCE(Status, '')) AS SearchText
            FROM pm_milestones
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0
        """),
        new("Label", "label", """
            SELECT Id, COALESCE(ProjectId, '') AS ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(LabelName, '') || ' ' || COALESCE(Color, '')) AS SearchText
            FROM pm_labels
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0
        """),
        new("ProjectMember", "member", """
            SELECT Id, ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(UserId, '') || ' ' || COALESCE(EmploymentId, '') || ' ' || COALESCE(RoleCode, '')) AS SearchText
            FROM pm_project_members
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0 AND IsActive = 1
        """),
        new("TaskComment", "comment", """
            SELECT Id, ProjectId, VersionNo, COALESCE(UpdatedTime, CreatedTime) AS UpdatedTime,
                   trim(COALESCE(Markdown, '') || ' ' || COALESCE(AuthorUserId, '')) AS SearchText
            FROM pm_task_comments
            WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0
        """)
    ];

    private async Task<IReadOnlyList<IndexDocument>> ReadDocumentsAsync(
        ISqlSugarClient db,
        DocumentDefinition definition,
        string tenantId,
        string appCode,
        string? aggregateId,
        CancellationToken cancellationToken)
    {
        var sql = definition.Sql + (aggregateId is null ? string.Empty : " AND Id = @aggregateId") + " ORDER BY Id";
        return await ReadRowsAsync(db, sql, reader => new IndexDocument(
            definition.ResultType,
            ReadString(reader, "Id"),
            ReadString(reader, "ProjectId"),
            ReadString(reader, "SearchText"),
            ReadLong(reader, "VersionNo"),
            ReadDateTime(reader, "UpdatedTime")), cancellationToken,
            ("@tenant", tenantId), ("@app", appCode), ("@aggregateId", aggregateId));
    }

    private async Task<IReadOnlyList<JournalRow>> ReadJournalBatchAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        long afterSequenceNo,
        long targetSequenceNo,
        int batchSize,
        CancellationToken cancellationToken) =>
        await ReadRowsAsync(db, $"""
            SELECT SequenceNo, AggregateType, AggregateId, VersionNo
            FROM pm_sync_journal
            WHERE TenantId = @tenant AND AppCode = @app AND SequenceNo > @after AND SequenceNo <= @target AND IsDeleted = 0
            ORDER BY SequenceNo
            LIMIT @limit
        """, reader => new JournalRow(
            ReadLong(reader, "SequenceNo"), ReadString(reader, "AggregateType"),
            ReadString(reader, "AggregateId"), ReadLong(reader, "VersionNo")), cancellationToken,
            ("@tenant", tenantId), ("@app", appCode), ("@after", afterSequenceNo), ("@target", targetSequenceNo), ("@limit", Math.Clamp(batchSize, 1, MaxBatchSize)));

    private async Task<IReadOnlyList<IndexHit>> ReadIndexHitsAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        string match,
        int limit,
        CancellationToken cancellationToken) =>
        await ReadRowsAsync(db, $"""
            SELECT ResultType, AggregateId
            FROM {IndexTable}
            WHERE TenantId = @tenant AND AppCode = @app AND SearchText MATCH @match
            ORDER BY rowid DESC
            LIMIT @limit
        """, reader => new IndexHit(ReadString(reader, "ResultType"), ReadString(reader, "AggregateId")), cancellationToken,
            ("@tenant", tenantId), ("@app", appCode), ("@match", match), ("@limit", limit));

    private async Task<long?> ReadIndexedVersionAsync(ISqlSugarClient db, string documentKey, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(db, $"SELECT VersionNo FROM {IndexTable} WHERE DocumentKey = @key LIMIT 1",
            reader => ReadLong(reader, "VersionNo"), cancellationToken, ("@key", documentKey));
        return rows.Count == 0 ? null : rows[0];
    }

    private async Task<long> GetJournalWatermarkAsync(ISqlSugarClient db, string tenantId, string appCode, CancellationToken cancellationToken)
    {
        var rows = await ReadRowsAsync(db,
            "SELECT COALESCE(MAX(SequenceNo), 0) AS SequenceNo FROM pm_sync_journal WHERE TenantId = @tenant AND AppCode = @app AND IsDeleted = 0",
            reader => ReadLong(reader, "SequenceNo"), cancellationToken, ("@tenant", tenantId), ("@app", appCode));
        return rows.Count == 0 ? 0 : rows[0];
    }

    private async Task UpsertDocumentAsync(ISqlSugarClient db, string tenantId, string appCode, IndexDocument document, CancellationToken cancellationToken)
    {
        var key = DocumentKey(tenantId, appCode, document.ResultType, document.AggregateId);
        await ExecuteNonQueryAsync(db, $"DELETE FROM {IndexTable} WHERE DocumentKey = @key", cancellationToken, ("@key", key));
        await ExecuteNonQueryAsync(db, $"""
            INSERT INTO {IndexTable}(DocumentKey, TenantId, AppCode, ResultType, AggregateId, ProjectId, VersionNo, SearchText, UpdatedTime)
            VALUES (@key, @tenant, @app, @type, @aggregateId, @projectId, @version, @text, @updated)
        """, cancellationToken,
            ("@key", key), ("@tenant", tenantId), ("@app", appCode),
            ("@type", document.ResultType), ("@aggregateId", document.AggregateId),
            ("@projectId", document.ProjectId), ("@version", document.VersionNo),
            ("@text", document.SearchText), ("@updated", document.UpdatedTime?.ToString("O", CultureInfo.InvariantCulture)));
    }

    private async Task<ProjectManagementSearchIndexStatusResponse> ReadStatusAsync(
        ISqlSugarClient db,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var states = await ReadRowsAsync(db, $"SELECT * FROM {StateTable} WHERE TenantId = @tenant AND AppCode = @app LIMIT 1",
            reader => new IndexState(
                ReadString(reader, "TenantId"), ReadString(reader, "AppCode"), ReadString(reader, "Mode"), ReadString(reader, "Status"),
                ReadLong(reader, "AppliedSequenceNo"), ReadLong(reader, "TargetSequenceNo"), ReadInt(reader, "FailureCount"),
                ReadNullableString(reader, "LastError"), ReadNullableString(reader, "OperationId"), ReadNullableDateTime(reader, "StartedTime"),
                ReadNullableDateTime(reader, "CompletedTime"), ReadDateTime(reader, "UpdatedTime")), cancellationToken,
            ("@tenant", tenantId), ("@app", appCode));
        var state = states.FirstOrDefault() ?? new IndexState(tenantId, appCode, "none", "Unavailable", 0, 0, 0, null, null, null, null, DateTime.UtcNow);
        var count = await ReadRowsAsync(db, $"SELECT COUNT(1) AS DocumentCount FROM {IndexTable} WHERE TenantId = @tenant AND AppCode = @app",
            reader => ReadInt(reader, "DocumentCount"), cancellationToken, ("@tenant", tenantId), ("@app", appCode));
        return new ProjectManagementSearchIndexStatusResponse(
            state.Status, state.Mode, state.AppliedSequenceNo, state.TargetSequenceNo,
            count.FirstOrDefault(), state.FailureCount, state.LastError, state.OperationId,
            state.StartedTime, state.CompletedTime, state.UpdatedTime);
    }

    private async Task WriteStateAsync(ISqlSugarClient db, IndexState state, CancellationToken cancellationToken) =>
        await ExecuteNonQueryAsync(db, $"""
            INSERT INTO {StateTable}(TenantId, AppCode, Mode, Status, AppliedSequenceNo, TargetSequenceNo, FailureCount, LastError, OperationId, StartedTime, CompletedTime, UpdatedTime)
            VALUES (@tenant, @app, @mode, @status, @applied, @target, @failures, @error, @operation, @started, @completed, @updated)
            ON CONFLICT(TenantId, AppCode) DO UPDATE SET
                Mode = excluded.Mode, Status = excluded.Status, AppliedSequenceNo = excluded.AppliedSequenceNo,
                TargetSequenceNo = excluded.TargetSequenceNo, FailureCount = excluded.FailureCount,
                LastError = excluded.LastError, OperationId = excluded.OperationId,
                StartedTime = excluded.StartedTime, CompletedTime = excluded.CompletedTime, UpdatedTime = excluded.UpdatedTime
        """, cancellationToken,
            ("@tenant", state.TenantId), ("@app", state.AppCode), ("@mode", state.Mode), ("@status", state.Status),
            ("@applied", state.AppliedSequenceNo), ("@target", state.TargetSequenceNo), ("@failures", state.FailureCount),
            ("@error", state.LastError), ("@operation", state.OperationId), ("@started", state.StartedTime?.ToString("O", CultureInfo.InvariantCulture)),
            ("@completed", state.CompletedTime?.ToString("O", CultureInfo.InvariantCulture)), ("@updated", state.UpdatedTime.ToString("O", CultureInfo.InvariantCulture)));

    private static async Task<List<T>> ReadRowsAsync<T>(
        ISqlSugarClient db,
        string sql,
        Func<DbDataReader, T> map,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Ado.Connection as DbConnection ?? throw new InvalidOperationException("SQLite connection does not support index reads.");
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        AddParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken)) rows.Add(map(reader));
        return rows;
    }

    private static async Task<int> ExecuteNonQueryAsync(
        ISqlSugarClient db,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        var connection = db.Ado.Connection as DbConnection ?? throw new InvalidOperationException("SQLite connection does not support index writes.");
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = db.Ado.Transaction as DbTransaction;
        AddParameters(command, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameters(DbCommand command, IEnumerable<(string Name, object? Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            if (value is null) continue;
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }

    private static string BuildFtsQuery(string keyword)
    {
        var tokens = keyword.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => item.Trim('"', '\'', '*', ':', '(', ')'))
            .Where(item => item.Length > 0)
            .Select(item => $"\"{item.Replace("\"", "\"\"", StringComparison.Ordinal)}\"*")
            .ToList();
        return tokens.Count == 0 ? string.Empty : string.Join(" AND ", tokens);
    }

    private static string DocumentKey(string tenantId, string appCode, string resultType, string aggregateId) =>
        $"{tenantId}\u001f{appCode}\u001f{resultType}\u001f{aggregateId}";

    private static string ReadString(DbDataReader reader, string name) =>
        reader[name] is DBNull ? string.Empty : Convert.ToString(reader[name], CultureInfo.InvariantCulture) ?? string.Empty;
    private static string? ReadNullableString(DbDataReader reader, string name) => reader[name] is DBNull ? null : Convert.ToString(reader[name], CultureInfo.InvariantCulture);
    private static long ReadLong(DbDataReader reader, string name) => reader[name] is DBNull ? 0 : Convert.ToInt64(reader[name], CultureInfo.InvariantCulture);
    private static int ReadInt(DbDataReader reader, string name) => reader[name] is DBNull ? 0 : Convert.ToInt32(reader[name], CultureInfo.InvariantCulture);
    private static DateTime ReadDateTime(DbDataReader reader, string name) => ReadNullableDateTime(reader, name) ?? DateTime.UtcNow;
    private static DateTime? ReadNullableDateTime(DbDataReader reader, string name)
    {
        if (reader[name] is DBNull) return null;
        if (reader[name] is DateTime dateTime) return dateTime;
        return DateTime.TryParse(Convert.ToString(reader[name], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed) ? parsed : null;
    }

    private static IndexImpact DeserializeImpact(string json)
    {
        try { return JsonSerializer.Deserialize<IndexImpact>(json) ?? new IndexImpact(200); }
        catch { return new IndexImpact(200); }
    }

    private sealed record IndexImpact(int BatchSize);
    private sealed record DocumentDefinition(string AggregateType, string ResultType, string Sql);
    private sealed record IndexDocument(string ResultType, string AggregateId, string ProjectId, string SearchText, long VersionNo, DateTime? UpdatedTime);
    private sealed record IndexHit(string ResultType, string AggregateId);
    private sealed record JournalRow(long SequenceNo, string AggregateType, string AggregateId, long VersionNo);
    private sealed record IndexedIds(
        IReadOnlyList<string> Projects,
        IReadOnlyList<string> Tasks,
        IReadOnlyList<string> Milestones,
        IReadOnlyList<string> Labels,
        IReadOnlyList<string> Members,
        IReadOnlyList<string> Comments);
    private sealed record IndexState(
        string TenantId,
        string AppCode,
        string Mode,
        string Status,
        long AppliedSequenceNo,
        long TargetSequenceNo,
        int FailureCount,
        string? LastError,
        string? OperationId,
        DateTime? StartedTime,
        DateTime? CompletedTime,
        DateTime UpdatedTime);

    private static ISugarQueryable<T> ApplyTime<T>(ISugarQueryable<T> source, DateTime? from, DateTime? to) where T : EntityBase
    {
        if (from.HasValue && to.HasValue)
        {
            var start = from.Value;
            var end = to.Value;
            source = source.Where(item =>
                (item.CreatedTime >= start && item.CreatedTime <= end) ||
                (item.UpdatedTime.HasValue && item.UpdatedTime.Value >= start && item.UpdatedTime.Value <= end));
        }
        else if (from.HasValue)
        {
            var start = from.Value;
            source = source.Where(item => item.CreatedTime >= start || (item.UpdatedTime.HasValue && item.UpdatedTime.Value >= start));
        }
        else if (to.HasValue)
        {
            var end = to.Value;
            source = source.Where(item => item.CreatedTime <= end || (item.UpdatedTime.HasValue && item.UpdatedTime.Value <= end));
        }
        return source;
    }

    private static string NormalizeScope(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "all" => "all",
        "projects" or "project" => "projects",
        "tasks" or "task" => "tasks",
        "milestones" or "milestone" => "milestones",
        "labels" or "label" => "labels",
        "members" or "member" or "people" => "members",
        "comments" or "comment" => "comments",
        _ => throw new ValidationException("搜索范围不受支持")
    };

    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Snippet(string value, string keyword)
    {
        var plainText = value.Replace("\0", string.Empty, StringComparison.Ordinal);
        var index = plainText.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return plainText.Length > 180 ? plainText[..180] : plainText;
        var start = Math.Max(0, index - 60);
        var length = Math.Min(plainText.Length - start, 180);
        return (start > 0 ? "…" : string.Empty) + plainText.Substring(start, length) + (start + length < plainText.Length ? "…" : string.Empty);
    }

    private static string Segment(string value) => Uri.EscapeDataString(value);
    private static string QueryValue(string value) => Uri.EscapeDataString(value);
    private string RequireUser() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
}
