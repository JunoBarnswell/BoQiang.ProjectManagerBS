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
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementAuditService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy) : IProjectManagementAuditService
{
    private const int MaximumTimeRangeDays = 92;
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

    public async Task<ProjectManagementAuditExportResponse> ExportAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default)
    {
        var activityQuery = await BuildQueryAsync(query, cancellationToken);
        var rows = await ApplySorting(activityQuery, query.Sorts).Take(10_000).ToListAsync(cancellationToken);
        var builder = new StringBuilder();
        builder.AppendLine("Id,ProjectId,AggregateType,AggregateId,ActivityType,Summary,TraceId,ActorUserId,CreatedTime");
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(',',
                Escape(row.Id), Escape(row.ProjectId), Escape(row.AggregateType), Escape(row.AggregateId),
                Escape(row.ActivityType), Escape(row.Summary), Escape(row.TraceId), Escape(row.ActorUserId), Escape(row.CreatedTime.ToString("O"))));
        }
        return new ProjectManagementAuditExportResponse($"project-management-audit-{DateTime.UtcNow:yyyyMMddHHmmss}.csv", Encoding.UTF8.GetBytes(builder.ToString()), rows.Count);
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
    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
