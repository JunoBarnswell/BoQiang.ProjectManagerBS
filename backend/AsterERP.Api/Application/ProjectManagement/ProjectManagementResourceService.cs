using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementResourceService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy accessPolicy,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementResourceService
{
    public async Task<IReadOnlyList<ProjectManagementResourceResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanViewProjectAsync(projectId, cancellationToken);
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementResourceEntity>()
            .Where(item => item.ProjectId == projectId && !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(200)
            .ToListAsync(cancellationToken);
        return items.Select(Map).ToArray();
    }

    public async Task<ProjectManagementResourceResponse> CreateAsync(string projectId, ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        Validate(request);
        var now = DateTime.UtcNow;
        var entity = new ProjectManagementResourceEntity
        {
            TenantId = Tenant(), AppCode = App(), ProjectId = projectId,
            ResourceName = Required(request.ResourceName, "资源名称不能为空"), ResourceUrl = RequiredUrl(request.ResourceUrl),
            Description = Optional(request.Description), VersionNo = 1, CreatedBy = User(), CreatedTime = now
        };
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "created", $"添加资源 {entity.ResourceName}", now, cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
        });
        await PublishAsync(entity, "project.resource.created", cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementResourceResponse> UpdateAsync(string projectId, string id, ProjectManagementResourceUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        Validate(request);
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        entity.ResourceName = Required(request.ResourceName, "资源名称不能为空");
        entity.ResourceUrl = RequiredUrl(request.ResourceUrl);
        entity.Description = Optional(request.Description);
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新资源 {entity.ResourceName}", entity.UpdatedTime.Value, cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        await PublishAsync(entity, "project.resource.updated", cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        await accessPolicy.EnsureCanManageProjectAsync(projectId, cancellationToken);
        var entity = await GetRequiredAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        entity.IsDeleted = true;
        entity.DeletedBy = User();
        entity.DeletedTime = DateTime.UtcNow;
        entity.UpdatedBy = entity.DeletedBy;
        entity.UpdatedTime = entity.DeletedTime;
        entity.VersionNo++;
        var db = databaseAccessor.GetCurrentDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "deleted", $"删除资源 {entity.ResourceName}", entity.UpdatedTime.Value, cancellationToken);
            await WriteSyncJournalAsync(entity, "deleted", cancellationToken);
        });
        await PublishAsync(entity, "project.resource.deleted", cancellationToken);
    }

    private async Task<ProjectManagementProjectEntity> EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var project = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return project.FirstOrDefault() ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementResourceEntity> GetRequiredAsync(string projectId, string id, CancellationToken cancellationToken)
    {
        var items = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementResourceEntity>()
            .Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return items.FirstOrDefault() ?? throw new NotFoundException("资源不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task WriteActivityAsync(ProjectManagementResourceEntity entity, string type, string summary, DateTime occurredAt, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), "ProjectResource", entity.Id, type, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), entity.ProjectId, Source: "User", OccurredAt: occurredAt), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(ProjectManagementResourceEntity entity, string operation, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(Tenant(), App(), "ProjectResource", entity.Id, entity.ProjectId, operation, entity.VersionNo, JsonSerializer.Serialize(entity), User(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }

    private async Task PublishAsync(ProjectManagementResourceEntity entity, string eventType, CancellationToken cancellationToken)
    {
        if (realtimePublisher is not null) await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(Tenant(), App(), "ProjectResource", entity.Id, eventType, entity.VersionNo, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), entity.ProjectId), cancellationToken);
    }

    private static void Validate(ProjectManagementResourceUpsertRequest request) { _ = Required(request.ResourceName, "资源名称不能为空"); _ = RequiredUrl(request.ResourceUrl); }
    private static string Required(string? value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string RequiredUrl(string? value) => Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri.ToString() : throw new ValidationException("资源链接必须是 http 或 https 地址");
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("资源已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static ProjectManagementResourceResponse Map(ProjectManagementResourceEntity entity) => new(entity.Id, entity.ProjectId, entity.ResourceName, entity.ResourceUrl, entity.Description, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime);
}
