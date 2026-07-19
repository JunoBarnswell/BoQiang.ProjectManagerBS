using System.Text;
using System.Text.Json;
using AsterERP.Api.Application.Common;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAuditService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementOperationWriter? operationWriter = null,
    IBackgroundJobManager? backgroundJobManager = null,
    IHostEnvironment? environment = null) : IProjectManagementAuditService
{
    private const int MaximumTimeRangeDays = 92;
    private const int MaximumExportRows = 100_000;
    private static readonly string[] DefaultExportFields = ["Id", "ProjectId", "AggregateType", "AggregateId", "ActivityType", "Summary", "TraceId", "ActorUserId", "CreatedTime"];
    private static readonly HashSet<string> ExportFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Id", "ProjectId", "AggregateType", "AggregateId", "ActivityType", "Summary", "TraceId", "ActorUserId", "CreatedTime", "Source", "FieldChanges"
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly IReadOnlyDictionary<string, Func<ISugarQueryable<ProjectManagementActivityEntity>, OrderByType, ISugarQueryable<ProjectManagementActivityEntity>>> Sorters =
        new Dictionary<string, Func<ISugarQueryable<ProjectManagementActivityEntity>, OrderByType, ISugarQueryable<ProjectManagementActivityEntity>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["createdTime"] = (activityQuery, order) => activityQuery.OrderBy(item => item.CreatedTime, order),
            ["projectId"] = (activityQuery, order) => activityQuery.OrderBy(item => item.ProjectId, order),
            ["aggregateType"] = (activityQuery, order) => activityQuery.OrderBy(item => item.AggregateType, order),
            ["activityType"] = (activityQuery, order) => activityQuery.OrderBy(item => item.ActivityType, order),
            ["actorUserId"] = (activityQuery, order) => activityQuery.OrderBy(item => item.ActorUserId, order)
        };

    public async Task<GridPageResult<ProjectManagementAuditItem>> QueryAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default)
    {
        var activityQuery = await BuildQueryAsync(query, cancellationToken);
        var total = new RefAsync<int>();
        var rows = await ApplySorting(activityQuery, query.Sorts)
            .ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<ProjectManagementAuditItem>
        {
            Total = total.Value,
            Items = (await MapManyAsync(rows, cancellationToken)).ToList()
        };
    }

    public async Task<ProjectManagementAuditDetail> GetDetailAsync(string id, CancellationToken cancellationToken = default)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        var tenantId = RequireTenant();
        var appCode = RequireApp();
        var auditId = NormalizeOptional(id, 128, "审计标识") ?? throw new ValidationException("审计标识不能为空");
        var db = databaseAccessor.GetProjectManagementDb();

        // 先经过已注册的 ORM 数据权限过滤器，再执行对象级项目授权；详情不能绕过 HAO-524 的可见性边界。
        var entity = (await db.Queryable<ProjectManagementActivityEntity>()
            .Where(item => item.Id == auditId && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault() ?? throw new NotFoundException("审计记录不存在", ErrorCodes.PlatformResourceNotFound);
        await accessPolicy.EnsureCanViewProjectAsync(entity.ProjectId, cancellationToken);

        var payload = DeserializePayload(entity.Remark);
        var batch = SanitizeBatch(payload?.Batch);
        var relatedActivities = await db.Queryable<ProjectManagementActivityEntity>()
            .Where(item => item.ProjectId == entity.ProjectId && item.TraceId == entity.TraceId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .OrderBy(item => item.Id, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var operations = await db.Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.TraceId == entity.TraceId && !item.IsDeleted)
            .OrderBy(item => item.StartedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var journals = await db.Queryable<ProjectManagementSyncJournalEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == entity.ProjectId && item.TraceId == entity.TraceId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Asc)
            .ToListAsync(cancellationToken);
        var device = journals.Where(item => !string.IsNullOrWhiteSpace(item.DeviceId)).OrderByDescending(item => item.CreatedTime).FirstOrDefault()?.DeviceId;
        var audit = Map(entity, device);

        return new ProjectManagementAuditDetail(
            audit,
            SanitizeFieldChanges(payload?.FieldChanges),
            batch,
            await CreateSnapshotAsync(db, entity, tenantId, appCode, cancellationToken),
            audit.IsSuccess ? null : audit.Summary,
            CreateRelatedEvents(entity, relatedActivities, operations, journals),
            CreateReferences(entity, batch, relatedActivities, operations, journals),
            currentUser.HasAsterErpPermission(PermissionCodes.SystemOperationLogQuery)
                ? $"/system/operation-logs?traceId={Uri.EscapeDataString(entity.TraceId)}"
                : null);
    }

    public async Task<ProjectManagementAuditExportResponse> ExportAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default)
    {
        return await GenerateCsvAsync(query, DefaultExportFields, false, cancellationToken);
    }

    public async Task<ProjectManagementAuditExportStartResponse> StartExportAsync(ProjectManagementAuditExportRequest request, CancellationToken cancellationToken = default)
    {
        var query = request.Query ?? new ProjectManagementAuditQuery();
        var fields = NormalizeExportFields(request.Fields);
        var includeSensitive = request.IncludeSensitive;
        if (includeSensitive && !currentUser.HasAsterErpPermission(PermissionCodes.SystemOperationLogQuery))
            throw new ValidationException("包含敏感字段的审计导出需要操作日志查询权限", ErrorCodes.PermissionDenied);

        // 先验证当前会话、时间范围和项目授权；实际数据读取在后台任务中再次经过同一 BuildQueryAsync。
        await BuildQueryAsync(query, cancellationToken);
        var operationId = Guid.NewGuid().ToString("N");
        var traceId = global::System.Diagnostics.Activity.Current?.Id ?? operationId;
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var impact = new AuditExportImpact(query, fields, includeSensitive, expiresAt, null, null, false, null);
        var writer = operationWriter ?? throw new InvalidOperationException("审计导出任务写入器未注册");
        var jobManager = backgroundJobManager ?? throw new InvalidOperationException("审计导出后台队列未注册");
        await writer.CreatePendingAsync(operationId, "audit.export", JsonSerializer.Serialize(impact, JsonOptions), traceId, cancellationToken);
        try
        {
            await jobManager.EnqueueAsync(new ProjectManagementOperationJobArgs(operationId, RequireTenant(), RequireApp(), RequireUser(), traceId));
        }
        catch (Exception exception)
        {
            await writer.FailAsync(operationId, $"审计导出入队失败：{exception.Message}", CancellationToken.None);
            throw;
        }

        return new ProjectManagementAuditExportStartResponse(operationId, traceId, expiresAt);
    }

    public async Task ExecuteExportAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var writer = operationWriter ?? throw new InvalidOperationException("审计导出任务写入器未注册");
        var operation = await GetOwnedExportOperationAsync(operationId, cancellationToken);
        var impact = DeserializeExportImpact(operation.ImpactJson);
        if (impact.IncludeSensitive && !currentUser.HasAsterErpPermission(PermissionCodes.SystemOperationLogQuery))
            throw new ValidationException("包含敏感字段的审计导出需要操作日志查询权限", ErrorCodes.PermissionDenied);
        var path = GetExportPath(operation.Id);
        try
        {
            await writer.StartAsync(operation.Id, "audit.export", operation.ImpactJson, operation.TraceId, cancellationToken);
            if (!await writer.ReportProgressAsync(operation.Id, "正在读取授权范围内的审计记录", 15, cancellationToken)) return;
            if (DateTime.UtcNow >= impact.ExpiresAt) throw new ValidationException("审计导出已超过有效期，请重新生成");
            var result = await GenerateCsvAsync(impact.Query, impact.Fields, impact.IncludeSensitive, cancellationToken,
                async progress =>
                {
                    if (!await writer.ReportProgressAsync(operation.Id, progress, 60, cancellationToken))
                        throw new OperationCanceledException("审计导出已取消", cancellationToken);
                });
            await writer.ReportProgressAsync(operation.Id, "正在写入导出文件", 80, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, result.Content, cancellationToken);
            if (await writer.IsCancellationRequestedAsync(operation.Id, cancellationToken))
            {
                TryDelete(path);
                await writer.CancelAsync(operation.Id, cancellationToken);
                return;
            }

            var completed = impact with { FileName = result.FileName, RowCount = result.Count, DownloadReady = true, CompletedAt = DateTime.UtcNow };
            await writer.CompleteWithImpactAsync(operation.Id, JsonSerializer.Serialize(completed, JsonOptions), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            TryDelete(path);
            throw;
        }
        catch (Exception exception)
        {
            TryDelete(path);
            await writer.FailAsync(operation.Id, $"审计导出生成失败：{exception.Message}", CancellationToken.None);
        }
    }

    public async Task<ProjectManagementAuditExportResponse> DownloadExportAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var operation = await GetOwnedExportOperationAsync(operationId, cancellationToken);
        if (!string.Equals(operation.Status, "Succeeded", StringComparison.Ordinal)) throw new ValidationException("审计导出尚未生成完成");
        var impact = DeserializeExportImpact(operation.ImpactJson);
        if (!impact.DownloadReady || string.IsNullOrWhiteSpace(impact.FileName)) throw new ValidationException("审计导出产物不可用");
        if (DateTime.UtcNow >= impact.ExpiresAt)
        {
            TryDelete(GetExportPath(operation.Id));
            throw new ValidationException("审计导出已过期，请重新生成");
        }
        var path = GetExportPath(operation.Id);
        if (!File.Exists(path)) throw new NotFoundException("审计导出文件不存在", ErrorCodes.PlatformResourceNotFound);
        return new ProjectManagementAuditExportResponse(impact.FileName, await File.ReadAllBytesAsync(path, cancellationToken), impact.RowCount ?? 0);
    }

    public async Task<GridPageResult<ProjectManagementOperationItem>> QueryOperationsAsync(ProjectManagementOperationQuery query, CancellationToken cancellationToken = default)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        RequireTenant();
        RequireApp();
        var operationQuery = databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.ActorUserId == RequireUser() && !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.OperationType)) operationQuery = operationQuery.Where(item => item.OperationType == query.OperationType.Trim());
        if (!string.IsNullOrWhiteSpace(query.Status)) operationQuery = operationQuery.Where(item => item.Status == query.Status.Trim());
        var total = new RefAsync<int>();
        var rows = await operationQuery.OrderBy(item => item.StartedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<ProjectManagementOperationItem>
        {
            Total = total.Value,
            Items = rows.Select(item => new ProjectManagementOperationItem(item.Id, item.OperationType, item.Status, item.Phase, item.ProgressPercent, item.IsCancellationRequested, item.ImpactJson, item.ErrorMessage, item.TraceId, item.ActorUserId, item.StartedTime, item.CompletedTime)).ToList()
        };
    }

    private async Task<ISugarQueryable<ProjectManagementActivityEntity>> BuildQueryAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken)
    {
        ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);
        RequireTenant();
        RequireApp();
        var (from, to) = NormalizeTimeRange(query.From, query.To);
        var projectId = NormalizeOptional(query.ProjectId, 128, "项目标识");
        if (projectId is not null) await accessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);

        // ProjectManagementDataPermissionFilterRegistrar 已为 ProjectManagementActivityEntity 注册 ORM 查询过滤器：
        // 这里必须从项目管理平台库查询，使成员授权变化立即在数据库端收敛，而不是在内存中再筛选。
        var activityQuery = databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementActivityEntity>()
            .Where(item => !item.IsDeleted);
        if (projectId is not null) activityQuery = activityQuery.Where(item => item.ProjectId == projectId);

        var aggregateType = NormalizeOptional(query.AggregateType, 128, "实体类型");
        if (aggregateType is not null) activityQuery = activityQuery.Where(item => item.AggregateType == aggregateType);
        var activityType = NormalizeOptional(query.ActivityType, 128, "操作类型");
        if (activityType is not null) activityQuery = activityQuery.Where(item => item.ActivityType == activityType);
        var actorUserId = NormalizeOptional(query.ActorUserId, 128, "操作者");
        if (actorUserId is not null) activityQuery = activityQuery.Where(item => item.ActorUserId == actorUserId);
        var actorRole = NormalizeOptional(query.ActorRole, 64, "项目角色");
        if (actorRole is not null)
        {
            activityQuery = actorRole.Equals("Owner", StringComparison.OrdinalIgnoreCase)
                ? activityQuery.Where(activity => SqlFunc.Subqueryable<ProjectManagementProjectEntity>()
                    .Where(project => project.Id == activity.ProjectId && project.OwnerUserId == activity.ActorUserId && !project.IsDeleted)
                    .Any())
                : activityQuery.Where(activity => SqlFunc.Subqueryable<ProjectManagementProjectMemberEntity>()
                    .Where(member => member.ProjectId == activity.ProjectId && member.UserId == activity.ActorUserId && member.RoleCode == actorRole && !member.IsDeleted)
                    .Any());
        }
        var source = NormalizeOptional(query.Source, 64, "来源方式");
        if (source is not null)
        {
            var sourceMarker = $"\"source\":\"{source}\"";
            activityQuery = activityQuery.Where(item => item.Remark != null && item.Remark.Contains(sourceMarker));
        }
        var sourceDeviceId = NormalizeOptional(query.SourceDeviceId, 128, "来源设备");
        if (sourceDeviceId is not null)
        {
            activityQuery = activityQuery.Where(activity => SqlFunc.Subqueryable<ProjectManagementSyncJournalEntity>()
                .Where(journal => journal.ProjectId == activity.ProjectId && journal.TraceId == activity.TraceId && journal.DeviceId == sourceDeviceId && !journal.IsDeleted)
                .Any());
        }
        if (query.IsSuccess.HasValue)
        {
            activityQuery = query.IsSuccess.Value
                ? activityQuery.Where(item => !item.ActivityType.Contains("failed") && (item.Summary == null || !item.Summary.Contains("失败")))
                : activityQuery.Where(item => item.ActivityType.Contains("failed") || (item.Summary != null && item.Summary.Contains("失败")));
        }
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            if (keyword.Length > 200) throw new ValidationException("审计关键字不能超过 200 个字符");
            activityQuery = activityQuery.Where(item => (item.Summary != null && item.Summary.Contains(keyword)) || item.ActorUserId.Contains(keyword) || item.AggregateId.Contains(keyword));
        }
        activityQuery = activityQuery.Where(item => item.CreatedTime >= from && item.CreatedTime <= to);
        return activityQuery;
    }

    private async Task<IReadOnlyList<ProjectManagementAuditItem>> MapManyAsync(
        IReadOnlyList<ProjectManagementActivityEntity> entities,
        CancellationToken cancellationToken)
    {
        if (entities.Count == 0) return [];
        var traceIds = entities.Select(item => item.TraceId).Distinct(StringComparer.Ordinal).ToList();
        var journals = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementSyncJournalEntity>()
            .Where(item => traceIds.Contains(item.TraceId) && item.DeviceId != null && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var deviceByProjectTrace = journals
            .Where(item => item.ProjectId is not null && !string.IsNullOrWhiteSpace(item.DeviceId))
            .GroupBy(item => ProjectTraceKey(item.ProjectId!, item.TraceId), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedTime).First().DeviceId, StringComparer.Ordinal);
        return entities.Select(item => Map(item, deviceByProjectTrace.GetValueOrDefault(ProjectTraceKey(item.ProjectId, item.TraceId)))).ToList();
    }

    private static ISugarQueryable<ProjectManagementActivityEntity> ApplySorting(
        ISugarQueryable<ProjectManagementActivityEntity> activityQuery,
        IReadOnlyList<GridSort>? sorts) =>
        GridSortApplier.Apply(activityQuery, sorts, Sorters, query => query
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .OrderBy(item => item.Id, OrderByType.Desc));

    private static (DateTime From, DateTime To) NormalizeTimeRange(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var end = to ?? now;
        var start = from ?? end.AddDays(-MaximumTimeRangeDays);
        if (start > end) throw new ValidationException("审计时间范围无效");
        if (end - start > TimeSpan.FromDays(MaximumTimeRangeDays))
            throw new ValidationException($"审计时间范围不能超过 {MaximumTimeRangeDays} 天，请缩小查询范围");
        return (start, end);
    }

    private static bool IsFailedActivity(ProjectManagementActivityEntity item) =>
        item.ActivityType.Contains("failed") ||
        (item.Summary != null && item.Summary.Contains("失败"));

    private static string ProjectTraceKey(string projectId, string traceId) => $"{projectId}\u001f{traceId}";

    private static string? NormalizeOptional(string? value, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var normalized = value.Trim();
        if (normalized.Length > maximum) throw new ValidationException($"{name}不能超过 {maximum} 个字符");
        return normalized;
    }

    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string RequireUser() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static ProjectManagementAuditItem Map(ProjectManagementActivityEntity entity, string? sourceDeviceId)
    {
        var source = "Business";
        if (!string.IsNullOrWhiteSpace(entity.Remark))
        {
            try { source = JsonSerializer.Deserialize<ProjectManagementActivityPayload>(entity.Remark, JsonOptions)?.Source ?? source; }
            catch (JsonException) { }
        }
        return new ProjectManagementAuditItem(entity.Id, entity.ProjectId, entity.AggregateType, entity.AggregateId, entity.ActivityType, entity.Summary, entity.TraceId, entity.ActorUserId, entity.CreatedTime, source, sourceDeviceId, !IsFailedActivity(entity));
    }

    private static ProjectManagementActivityPayload? DeserializePayload(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return JsonSerializer.Deserialize<ProjectManagementActivityPayload>(value, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<ProjectManagementActivityFieldChange> SanitizeFieldChanges(IReadOnlyList<ProjectManagementActivityFieldChange>? changes) =>
        (changes ?? []).Select(change =>
        {
            var sensitive = change.IsSensitive || IsSensitiveField(change.Field);
            return change with
            {
                Before = sensitive ? Mask(change.Before) : LimitDetailValue(change.Before),
                After = sensitive ? Mask(change.After) : LimitDetailValue(change.After),
                IsSensitive = sensitive
            };
        }).ToList();

    private static ProjectManagementActivityBatch? SanitizeBatch(ProjectManagementActivityBatch? batch)
    {
        if (batch is null) return null;
        return batch with
        {
            Details = (batch.Details ?? []).Take(200).Select(item => item with
            {
                Summary = LimitDetailValue(item.Summary),
                FieldChanges = SanitizeFieldChanges(item.FieldChanges)
            }).ToList()
        };
    }

    private static string? LimitDetailValue(string? value) => value is null ? null : value.Length <= 4_000 ? value : $"{value[..4_000]}…";

    private static bool IsSensitiveField(string field) =>
        field.Contains("password", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("token", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("privatekey", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("credential", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("authorization", StringComparison.OrdinalIgnoreCase) ||
        field.Contains("密码", StringComparison.Ordinal) ||
        field.Contains("密钥", StringComparison.Ordinal) ||
        field.Contains("令牌", StringComparison.Ordinal);

    private static string? Mask(string? value) => value is null ? null : "[已脱敏]";

    private static async Task<ProjectManagementAuditEntitySnapshot> CreateSnapshotAsync(
        ISqlSugarClient db,
        ProjectManagementActivityEntity entity,
        string tenantId,
        string appCode,
        CancellationToken cancellationToken)
    {
        var isDeleted = entity.AggregateType switch
        {
            "Task" => !(await db.Queryable<ProjectManagementTaskEntity>()
                .Where(item => item.Id == entity.AggregateId && item.ProjectId == entity.ProjectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                .AnyAsync(cancellationToken)),
            "Project" => !(await db.Queryable<ProjectManagementProjectEntity>()
                .Where(item => item.Id == entity.AggregateId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                .AnyAsync(cancellationToken)),
            _ => false
        };
        return new ProjectManagementAuditEntitySnapshot(entity.ProjectId, entity.AggregateType, entity.AggregateId, entity.Summary, isDeleted);
    }

    private static IReadOnlyList<ProjectManagementAuditRelatedEvent> CreateRelatedEvents(
        ProjectManagementActivityEntity current,
        IReadOnlyList<ProjectManagementActivityEntity> activities,
        IReadOnlyList<ProjectManagementOperationEntity> operations,
        IReadOnlyList<ProjectManagementSyncJournalEntity> journals)
    {
        var events = new List<ProjectManagementAuditRelatedEvent>();
        events.AddRange(activities.Select(item => new ProjectManagementAuditRelatedEvent(
            item.Id, "Activity", Causality(current, item.CreatedTime, item.Id), item.AggregateType, item.AggregateId,
            item.ActivityType, item.Summary, IsFailedActivity(item) ? "Failed" : "Succeeded", item.CreatedTime)));
        events.AddRange(operations.Select(item => new ProjectManagementAuditRelatedEvent(
            item.Id, "Operation", Causality(current, item.StartedTime, item.Id), null, null,
            item.OperationType, null, item.Status, item.StartedTime)));
        events.AddRange(journals.Select(item => new ProjectManagementAuditRelatedEvent(
            item.Id, "SyncJournal", Causality(current, item.CreatedTime, item.Id), item.AggregateType, item.AggregateId,
            item.Operation, null, null, item.CreatedTime)));
        return events.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id, StringComparer.Ordinal).ToList();
    }

    private static string Causality(ProjectManagementActivityEntity current, DateTime occurredAt, string id)
    {
        if (id == current.Id) return "Current";
        if (occurredAt < current.CreatedTime || occurredAt == current.CreatedTime && string.CompareOrdinal(id, current.Id) < 0) return "Preceded";
        return "Followed";
    }

    private static IReadOnlyList<ProjectManagementAuditReference> CreateReferences(
        ProjectManagementActivityEntity current,
        ProjectManagementActivityBatch? batch,
        IReadOnlyList<ProjectManagementActivityEntity> activities,
        IReadOnlyList<ProjectManagementOperationEntity> operations,
        IReadOnlyList<ProjectManagementSyncJournalEntity> journals)
    {
        var references = new List<ProjectManagementAuditReference>();
        if (batch is not null) references.Add(new ProjectManagementAuditReference("BatchOperation", batch.OperationId));
        references.AddRange(operations.Select(item => new ProjectManagementAuditReference("Operation", item.Id, item.OperationType)));
        references.AddRange(journals.Select(item => new ProjectManagementAuditReference("SyncChange", item.Id, $"{item.AggregateType}/{item.AggregateId}")));
        foreach (var activity in activities.Append(current))
        {
            var kind = activity.ActivityType.Contains("backup", StringComparison.OrdinalIgnoreCase) ? "Backup"
                : activity.ActivityType.Contains("import", StringComparison.OrdinalIgnoreCase) ? "Import"
                : activity.ActivityType.Contains("approval", StringComparison.OrdinalIgnoreCase) || activity.ActivityType.Contains("workflow", StringComparison.OrdinalIgnoreCase) ? "Workflow"
                : null;
            if (kind is not null) references.Add(new ProjectManagementAuditReference(kind, activity.AggregateId, activity.ActivityType));
        }
        return references.DistinctBy(item => (item.Kind, item.Id)).ToList();
    }

    private async Task<ProjectManagementAuditExportResponse> GenerateCsvAsync(
        ProjectManagementAuditQuery query,
        IReadOnlyList<string> fields,
        bool includeSensitive,
        CancellationToken cancellationToken,
        Func<string, Task>? reportProgress = null)
    {
        var activityQuery = await BuildQueryAsync(query, cancellationToken);
        var rows = await ApplySorting(activityQuery, query.Sorts).Take(MaximumExportRows).ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        AppendCsvRow(builder, fields);
        for (var index = 0; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = rows[index];
            var payload = DeserializePayload(row.Remark);
            var values = fields.Select(field => field switch
            {
                "Id" => row.Id,
                "ProjectId" => row.ProjectId,
                "AggregateType" => row.AggregateType,
                "AggregateId" => row.AggregateId,
                "ActivityType" => row.ActivityType,
                "Summary" => row.Summary,
                "TraceId" => row.TraceId,
                "ActorUserId" => row.ActorUserId,
                "CreatedTime" => row.CreatedTime.ToString("O"),
                "Source" => payload?.Source ?? "Business",
                "FieldChanges" => JsonSerializer.Serialize(includeSensitive ? LimitFieldChanges(payload?.FieldChanges) : SanitizeFieldChanges(payload?.FieldChanges), JsonOptions),
                _ => string.Empty
            });
            AppendCsvRow(builder, values);
            if (reportProgress is not null && (index + 1) % 1_000 == 0) await reportProgress($"已处理 {index + 1} 条审计记录");
        }
        return new ProjectManagementAuditExportResponse(
            $"project-management-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.csv",
            Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(builder.ToString())).ToArray(),
            rows.Count);
    }

    private static IReadOnlyList<string> NormalizeExportFields(IReadOnlyList<string>? fields)
    {
        var selected = (fields ?? DefaultExportFields).Select(field => field.Trim()).Where(field => field.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (selected.Count == 0) selected.AddRange(DefaultExportFields);
        if (selected.Any(field => !ExportFields.Contains(field))) throw new ValidationException("审计导出包含不支持的字段");
        return selected.Select(field => ExportFields.First(allowed => allowed.Equals(field, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private static IReadOnlyList<ProjectManagementActivityFieldChange> LimitFieldChanges(IReadOnlyList<ProjectManagementActivityFieldChange>? changes) =>
        (changes ?? []).Take(200).Select(change => change with { Before = LimitDetailValue(change.Before), After = LimitDetailValue(change.After) }).ToList();

    private static void AppendCsvRow(StringBuilder builder, IEnumerable<string?> values) => builder.AppendLine(string.Join(',', values.Select(Escape)));

    private static string Escape(string? value)
    {
        var normalized = value ?? string.Empty;
        if (normalized.Length > 0 && normalized[0] is '=' or '+' or '-' or '@') normalized = $"'{normalized}";
        return $"\"{normalized.Replace("\"", "\"\"")}\"";
    }

    private async Task<ProjectManagementOperationEntity> GetOwnedExportOperationAsync(string operationId, CancellationToken cancellationToken) =>
        (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
            .Where(item => item.Id == operationId.Trim() && item.TenantId == RequireTenant() && item.AppCode == RequireApp() && item.ActorUserId == RequireUser() && item.OperationType == "audit.export" && !item.IsDeleted)
            .Take(1).ToListAsync(cancellationToken)).FirstOrDefault()
        ?? throw new NotFoundException("审计导出任务不存在或无权访问", ErrorCodes.PlatformResourceNotFound);

    private static AuditExportImpact DeserializeExportImpact(string json)
    {
        try { return JsonSerializer.Deserialize<AuditExportImpact>(json, JsonOptions) ?? throw new JsonException(); }
        catch (JsonException) { throw new ValidationException("审计导出任务数据损坏"); }
    }

    private string GetExportPath(string operationId)
    {
        var root = Path.GetFullPath(Path.Combine((environment ?? throw new InvalidOperationException("审计导出宿主环境未注册")).ContentRootPath, "data", "project-management-audit-exports", RequireTenant(), RequireApp()));
        var path = Path.GetFullPath(Path.Combine(root, $"{operationId}.csv"));
        var relative = Path.GetRelativePath(root, path);
        if (relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative)) throw new ValidationException("审计导出路径不合法");
        return path;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private sealed record AuditExportImpact(
        ProjectManagementAuditQuery Query,
        IReadOnlyList<string> Fields,
        bool IncludeSensitive,
        DateTime ExpiresAt,
        string? FileName,
        int? RowCount,
        bool DownloadReady,
        DateTime? CompletedAt);
}
