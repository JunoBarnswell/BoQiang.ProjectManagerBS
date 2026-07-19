using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementActivityService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser) : IProjectManagementActivityService
{
    public async Task AppendAsync(ProjectManagementActivityEvent activity, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(activity.ProjectId)) throw new ValidationException("活动必须绑定项目");
        var db = databaseAccessor.GetProjectManagementDb();
        await db.Insertable(new ProjectManagementActivityEntity
        {
            TenantId = RequireTenant(), AppCode = RequireApp(), ProjectId = Required(activity.ProjectId), AggregateType = Required(activity.AggregateType), AggregateId = Required(activity.AggregateId), ActivityType = Required(activity.ActivityType), Summary = activity.Summary, TraceId = Required(activity.TraceId), ActorUserId = Required(activity.ActorUserId), CreatedBy = Required(activity.ActorUserId), CreatedTime = DateTime.UtcNow
        }).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectManagementActivityResponse>> QueryAsync(string projectId, int limit = 100, CancellationToken cancellationToken = default)
    {
        RequireTenant(); RequireApp();
        var rows = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementActivityEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).Take(Math.Clamp(limit, 1, 500)).ToListAsync(cancellationToken);
        return rows.Select(item => new ProjectManagementActivityResponse(item.Id, item.ProjectId, item.AggregateType, item.AggregateId, item.ActivityType, item.Summary, item.TraceId, item.ActorUserId, item.CreatedTime)).ToList();
    }

    private string RequireTenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private static string RequireApp() => ProjectManagementPlatformScope.AppCode;
    private static string Required(string? value) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException("活动字段不能为空") : value.Trim();
}
