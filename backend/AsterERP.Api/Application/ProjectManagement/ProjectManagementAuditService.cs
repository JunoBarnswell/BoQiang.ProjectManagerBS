using System.Text;
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
    public async Task<GridPageResult<ProjectManagementAuditItem>> QueryAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default)
    {
        var activityQuery = await BuildQueryAsync(query, cancellationToken);
        var total = new RefAsync<int>();
        var rows = await activityQuery
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(1, query.PageIndex), Math.Clamp(query.PageSize, 1, 200), total, cancellationToken);
        return new GridPageResult<ProjectManagementAuditItem>
        {
            Total = total.Value,
            Items = rows.Select(Map).ToList()
        };
    }

    public async Task<ProjectManagementAuditExportResponse> ExportAsync(ProjectManagementAuditQuery query, CancellationToken cancellationToken = default)
    {
        var activityQuery = await BuildQueryAsync(query, cancellationToken);
        var rows = await activityQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(10_000).ToListAsync(cancellationToken);
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
        RequireTenant();
        RequireApp();
        var operationQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementOperationEntity>()
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
        RequireTenant();
        RequireApp();
        if (query.From.HasValue && query.To.HasValue && query.From > query.To) throw new ValidationException("审计时间范围无效");
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) await accessPolicy.EnsureCanViewProjectAsync(query.ProjectId, cancellationToken);
        var activityQuery = databaseAccessor.GetCurrentDb().Queryable<ProjectManagementActivityEntity>()
            .Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.ProjectId)) activityQuery = activityQuery.Where(item => item.ProjectId == query.ProjectId);
        if (!string.IsNullOrWhiteSpace(query.AggregateType)) activityQuery = activityQuery.Where(item => item.AggregateType == query.AggregateType.Trim());
        if (!string.IsNullOrWhiteSpace(query.ActivityType)) activityQuery = activityQuery.Where(item => item.ActivityType == query.ActivityType.Trim());
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            if (keyword.Length > 200) throw new ValidationException("审计关键字不能超过 200 个字符");
            activityQuery = activityQuery.Where(item => (item.Summary != null && item.Summary.Contains(keyword)) || item.ActorUserId.Contains(keyword) || item.AggregateId.Contains(keyword));
        }
        if (query.From.HasValue) activityQuery = activityQuery.Where(item => item.CreatedTime >= query.From.Value);
        if (query.To.HasValue) activityQuery = activityQuery.Where(item => item.CreatedTime <= query.To.Value);
        return activityQuery;
    }

    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string RequireApp() => currentUser.GetAsterErpAppCode()?.Trim() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string RequireUser() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static ProjectManagementAuditItem Map(ProjectManagementActivityEntity entity) => new(entity.Id, entity.ProjectId, entity.AggregateType, entity.AggregateId, entity.ActivityType, entity.Summary, entity.TraceId, entity.ActorUserId, entity.CreatedTime);
    private static string Escape(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
}
