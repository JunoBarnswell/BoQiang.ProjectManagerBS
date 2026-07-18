using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementLabelService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementTaskLabelMutation? labelMutation = null,
    IProjectManagementActivityWriter? activityWriter = null) : IProjectManagementLabelService
{
    public async Task<IReadOnlyList<ProjectManagementLabelResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var tenantId = Tenant();
        var appCode = App();
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementLabelEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && (item.ProjectId == null || item.ProjectId == projectId))
            .OrderBy(item => item.ProjectId, OrderByType.Asc)
            .OrderBy(item => item.LabelName, OrderByType.Asc)
            .ToListAsync(cancellationToken))
            .Select(Map)
            .ToList();
    }

    public async Task<IReadOnlyList<ProjectManagementLabelResponse>> QueryPublicAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = Tenant();
        var appCode = App();
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementLabelEntity>()
            .Where(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == null && !item.IsDeleted)
            .OrderBy(item => item.LabelName, OrderByType.Asc)
            .ToListAsync(cancellationToken))
            .Select(Map)
            .ToList();
    }

    public Task<ProjectManagementLabelResponse> CreateAsync(string projectId, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
        => CreateScopedAsync(projectId, request, cancellationToken);

    public Task<ProjectManagementLabelResponse> CreatePublicAsync(ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
        => CreateScopedAsync(null, request, cancellationToken);

    public Task<ProjectManagementLabelResponse> UpdateAsync(string projectId, string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
        => UpdateScopedAsync(projectId, id, request, cancellationToken);

    public Task<ProjectManagementLabelResponse> UpdatePublicAsync(string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
        => UpdateScopedAsync(null, id, request, cancellationToken);

    public Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
        => DeleteScopedAsync(projectId, id, versionNo, cancellationToken);

    public Task DeletePublicAsync(string id, long versionNo, CancellationToken cancellationToken = default)
        => DeleteScopedAsync(null, id, versionNo, cancellationToken);

    public async Task<IReadOnlyList<ProjectManagementTaskLabelResponse>> QueryTaskLabelsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = Tenant();
        var appCode = App();
        var links = await db.Queryable<ProjectManagementTaskLabelEntity>()
            .Where(item => item.TaskId == task.Id && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .ToListAsync(cancellationToken);
        var labelIds = links.Select(item => item.LabelId).Distinct(StringComparer.Ordinal).ToList();
        var labels = labelIds.Count == 0 ? [] : await db.Queryable<ProjectManagementLabelEntity>()
            .Where(item => labelIds.Contains(item.Id) && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted && (item.ProjectId == null || item.ProjectId == task.ProjectId))
            .ToListAsync(cancellationToken);
        var byId = labels.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return links.Where(link => byId.ContainsKey(link.LabelId)).Select(link =>
        {
            var label = byId[link.LabelId];
            return new ProjectManagementTaskLabelResponse(link.Id, link.TaskId, label.Id, label.LabelName, label.Color);
        }).ToList();
    }

    public async Task SetTaskLabelsAsync(string taskId, ProjectManagementTaskLabelSetRequest request, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        EnsureVersion(task.VersionNo, request.VersionNo);
        var db = databaseAccessor.GetCurrentDb();
        var now = DateTime.UtcNow;
        var actorUserId = User();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(
                db, task, request.LabelIds, Tenant(), App(), actorUserId, now, cancellationToken);
            task.VersionNo++;
            task.UpdatedBy = actorUserId;
            task.UpdatedTime = now;
            await db.Updateable(task)
                .UpdateColumns(item => new { item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(task.ProjectId, task.Id, "task.labels.replaced", $"更新任务标签（{request.LabelIds.Count} 项）", cancellationToken);
        });
    }

    private async Task<ProjectManagementLabelResponse> CreateScopedAsync(string? projectId, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken)
    {
        if (projectId is not null) await EnsureProjectAsync(projectId, cancellationToken);
        var name = Required(request.LabelName, "标签名称不能为空");
        var color = NormalizeColor(request.Color);
        var db = databaseAccessor.GetCurrentDb();
        await EnsureNameAvailableAsync(projectId, name, null, cancellationToken);
        var entity = new ProjectManagementLabelEntity
        {
            TenantId = Tenant(),
            AppCode = App(),
            ProjectId = projectId,
            LabelName = name,
            Color = color,
            CreatedBy = User(),
            CreatedTime = DateTime.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteActivityAsync(projectId, entity.Id, "label.created", $"创建{ScopeName(projectId)}标签 {entity.LabelName}", cancellationToken);
        return Map(entity);
    }

    private async Task<ProjectManagementLabelResponse> UpdateScopedAsync(string? projectId, string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken)
    {
        if (projectId is not null) await EnsureProjectAsync(projectId, cancellationToken);
        var entity = await GetRequiredLabelAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        var name = Required(request.LabelName, "标签名称不能为空");
        var color = NormalizeColor(request.Color);
        await EnsureNameAvailableAsync(projectId, name, entity.Id, cancellationToken);
        entity.LabelName = name;
        entity.Color = color;
        entity.VersionNo++;
        entity.UpdatedBy = User();
        entity.UpdatedTime = DateTime.UtcNow;
        await databaseAccessor.GetCurrentDb().Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteActivityAsync(projectId, entity.Id, "label.updated", $"更新{ScopeName(projectId)}标签 {entity.LabelName}", cancellationToken);
        return Map(entity);
    }

    private async Task DeleteScopedAsync(string? projectId, string id, long versionNo, CancellationToken cancellationToken)
    {
        if (projectId is not null) await EnsureProjectAsync(projectId, cancellationToken);
        var entity = await GetRequiredLabelAsync(projectId, id, cancellationToken);
        EnsureVersion(entity.VersionNo, versionNo);
        var db = databaseAccessor.GetCurrentDb();
        var now = DateTime.UtcNow;
        var actorUserId = User();
        var tenantId = Tenant();
        var appCode = App();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            var links = await db.Queryable<ProjectManagementTaskLabelEntity>()
                .Where(item => item.LabelId == entity.Id && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
                .ToListAsync(cancellationToken);
            entity.IsDeleted = true;
            entity.DeletedBy = actorUserId;
            entity.DeletedTime = now;
            entity.UpdatedBy = actorUserId;
            entity.UpdatedTime = now;
            entity.VersionNo++;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            if (links.Count > 0)
            {
                foreach (var link in links)
                {
                    link.IsDeleted = true;
                    link.DeletedBy = actorUserId;
                    link.DeletedTime = now;
                    link.UpdatedBy = actorUserId;
                    link.UpdatedTime = now;
                    link.VersionNo++;
                }
                await db.Updateable(links)
                    .UpdateColumns(item => new { item.IsDeleted, item.DeletedBy, item.DeletedTime, item.UpdatedBy, item.UpdatedTime, item.VersionNo })
                    .ExecuteCommandAsync(cancellationToken);
            }
            await WriteDeletionActivitiesAsync(projectId, entity.Id, entity.LabelName, links, cancellationToken);
        });
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken)
    {
        var tenantId = Tenant();
        var appCode = App();
        if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == projectId && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .AnyAsync(cancellationToken))
            throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken)
    {
        var tenantId = Tenant();
        var appCode = App();
        var task = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>()
            .Where(item => item.Id == id && item.TenantId == tenantId && item.AppCode == appCode && !item.IsDeleted)
            .Take(1)
            .ToListAsync(cancellationToken);
        return task.FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task<ProjectManagementLabelEntity> GetRequiredLabelAsync(string? projectId, string id, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = Tenant();
        var appCode = App();
        List<ProjectManagementLabelEntity> labels = projectId is null
            ? await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.Id == id && item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == null && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)
            : await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.Id == id && item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken);
        return labels.FirstOrDefault() ?? throw new NotFoundException("标签不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private async Task EnsureNameAvailableAsync(string? projectId, string name, string? ignoredId, CancellationToken cancellationToken)
    {
        var db = databaseAccessor.GetCurrentDb();
        var tenantId = Tenant();
        var appCode = App();
        var exists = projectId is null
            ? await db.Queryable<ProjectManagementLabelEntity>().AnyAsync(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == null && item.LabelName == name && item.Id != ignoredId && !item.IsDeleted, cancellationToken)
            : await db.Queryable<ProjectManagementLabelEntity>().AnyAsync(item => item.TenantId == tenantId && item.AppCode == appCode && item.ProjectId == projectId && item.LabelName == name && item.Id != ignoredId && !item.IsDeleted, cancellationToken);
        if (exists) throw new ValidationException($"{ScopeName(projectId)}标签名称已存在");
    }

    private async Task WriteActivityAsync(string? projectId, string aggregateId, string activityType, string summary, CancellationToken cancellationToken)
    {
        // 活动流强制绑定项目；公共标签没有项目归属，因此不写入项目活动流。
        if (activityWriter is null || string.IsNullOrWhiteSpace(projectId)) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(Tenant(), App(), "Label", aggregateId, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), User(), projectId), cancellationToken);
    }

    private async Task WriteDeletionActivitiesAsync(
        string? projectId,
        string labelId,
        string labelName,
        IReadOnlyCollection<ProjectManagementTaskLabelEntity> links,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;

        var affectedProjects = projectId is null
            ? links.Select(link => link.ProjectId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList()
            : [projectId];
        foreach (var affectedProjectId in affectedProjects)
        {
            var relationCount = links.Count(link => string.Equals(link.ProjectId, affectedProjectId, StringComparison.Ordinal));
            await WriteActivityAsync(
                affectedProjectId,
                labelId,
                "label.deleted",
                $"删除{ScopeName(projectId)}标签 {labelName}，解除 {relationCount} 个任务关联",
                cancellationToken);
        }
    }

    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static string NormalizeColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !global::System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#[0-9A-Fa-f]{6}$"))
            throw new ValidationException("标签颜色必须是六位十六进制颜色");
        return value.Trim().ToUpperInvariant();
    }
    private static string ScopeName(string? projectId) => projectId is null ? "公共" : "项目";
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("标签或任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementLabelResponse Map(ProjectManagementLabelEntity entity) => new(entity.Id, entity.ProjectId, entity.ProjectId is null ? ProjectManagementLabelScopes.Public : ProjectManagementLabelScopes.Project, entity.LabelName, entity.Color, entity.VersionNo);
}
