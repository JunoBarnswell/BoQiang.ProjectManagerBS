using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementLabelService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementTaskLabelMutation? labelMutation = null) : IProjectManagementLabelService
{
    public async Task<IReadOnlyList<ProjectManagementLabelResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        return (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementLabelEntity>()
            .Where(item => !item.IsDeleted && (item.ProjectId == null || item.ProjectId == projectId))
            .OrderBy(item => item.LabelName)
            .ToListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<ProjectManagementLabelResponse> CreateAsync(string projectId, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var name = Required(request.LabelName, "标签名称不能为空");
        ValidateColor(request.Color);
        var db = databaseAccessor.GetCurrentDb();
        if (await db.Queryable<ProjectManagementLabelEntity>().AnyAsync(item => item.ProjectId == projectId && item.LabelName == name && !item.IsDeleted, cancellationToken)) throw new ValidationException("项目标签已存在");
        var entity = new ProjectManagementLabelEntity { TenantId = Tenant(), AppCode = App(), ProjectId = projectId, LabelName = name, Color = request.Color.Trim(), CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementLabelResponse> UpdateAsync(string projectId, string id, ProjectManagementLabelUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("标签不存在", ErrorCodes.PlatformResourceNotFound);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        entity.LabelName = Required(request.LabelName, "标签名称不能为空"); ValidateColor(request.Color); entity.Color = request.Color.Trim(); entity.VersionNo++; entity.UpdatedBy = User(); entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementLabelEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("标签不存在", ErrorCodes.PlatformResourceNotFound);
        EnsureVersion(entity.VersionNo, versionNo);
        if (await db.Queryable<ProjectManagementTaskLabelEntity>().AnyAsync(item => item.LabelId == id && !item.IsDeleted, cancellationToken)) throw new ValidationException("标签仍被任务引用，不能删除");
        entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.VersionNo++;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectManagementTaskLabelResponse>> QueryTaskLabelsAsync(string taskId, CancellationToken cancellationToken = default)
    {
        var task = await EnsureTaskAsync(taskId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var links = await db.Queryable<ProjectManagementTaskLabelEntity>().Where(item => item.TaskId == task.Id && !item.IsDeleted).ToListAsync(cancellationToken);
        var labelIds = links.Select(item => item.LabelId).Distinct(StringComparer.Ordinal).ToList();
        var labels = labelIds.Count == 0 ? [] : await db.Queryable<ProjectManagementLabelEntity>().Where(item => labelIds.Contains(item.Id) && !item.IsDeleted).ToListAsync(cancellationToken);
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
        var db = databaseAccessor.GetCurrentDb();
        var current = (await db.Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == taskId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).First();
        EnsureVersion(current.VersionNo, request.VersionNo);
        var now = DateTime.UtcNow;
        var actorUserId = User();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await (labelMutation ?? new ProjectManagementTaskLabelMutation()).ReplaceAsync(
                db, task, request.LabelIds, Tenant(), App(), actorUserId, now, cancellationToken);
            current.VersionNo++;
            current.UpdatedBy = actorUserId;
            current.UpdatedTime = now;
            await db.Updateable(current)
                .UpdateColumns(item => new { item.VersionNo, item.UpdatedBy, item.UpdatedTime })
                .ExecuteCommandAsync(cancellationToken);
        });
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken) { if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).AnyAsync(cancellationToken)) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound); Tenant(); App(); }
    private async Task<ProjectManagementTaskEntity> EnsureTaskAsync(string id, CancellationToken cancellationToken) => (await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementTaskEntity>().Where(item => item.Id == id && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("任务不存在", ErrorCodes.PlatformResourceNotFound);
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用");
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");
    private static string Required(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();
    private static void ValidateColor(string value) { if (string.IsNullOrWhiteSpace(value) || !global::System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), "^#[0-9A-Fa-f]{6}$")) throw new ValidationException("标签颜色必须是六位十六进制颜色"); }
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("标签或任务已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementLabelResponse Map(ProjectManagementLabelEntity entity) => new(entity.Id, entity.ProjectId, entity.LabelName, entity.Color, entity.VersionNo);
}
