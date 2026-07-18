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

public sealed class ProjectManagementSavedViewService(IWorkspaceDatabaseAccessor databaseAccessor, ICurrentUser currentUser, ProjectManagementAccessPolicy? accessPolicy = null) : IProjectManagementSavedViewService
{
    private static readonly string[] ViewKeys = ["tree", "list", "card", "board", "gantt", "calendar"];

    public async Task<IReadOnlyList<ProjectManagementSavedViewResponse>> QueryAsync(string projectId, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var user = User();
        var rows = await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementSavedViewEntity>().Where(item => item.ProjectId == projectId && !item.IsDeleted && (item.IsShared || item.OwnerUserId == user)).OrderBy(item => item.IsDefault, OrderByType.Desc).OrderBy(item => item.ViewName).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ProjectManagementSavedViewResponse> CreateAsync(string projectId, ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var queryJson = Validate(request);
        if (request.IsShared) await Policy().EnsureCanManageProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        if (await db.Queryable<ProjectManagementSavedViewEntity>().AnyAsync(item => item.ProjectId == projectId && item.OwnerUserId == User() && item.ViewName == request.ViewName.Trim() && !item.IsDeleted, cancellationToken)) throw new ValidationException("同名视图已存在");
        if (request.IsDefault) await ClearDefaultAsync(projectId, request.IsShared, cancellationToken);
        var entity = new ProjectManagementSavedViewEntity { TenantId = Tenant(), AppCode = App(), ProjectId = projectId, ViewName = request.ViewName.Trim(), ViewKey = request.ViewKey, QueryJson = queryJson, OwnerUserId = User(), IsShared = request.IsShared, IsDefault = request.IsDefault, CreatedBy = User(), CreatedTime = DateTime.UtcNow };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementSavedViewResponse> UpdateAsync(string projectId, string id, ProjectManagementSavedViewUpsertRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var queryJson = Validate(request);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementSavedViewEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("保存视图不存在", ErrorCodes.PlatformResourceNotFound);
        if (!string.Equals(entity.OwnerUserId, User(), StringComparison.OrdinalIgnoreCase) && !entity.IsShared) throw new ValidationException("不能编辑其他用户的视图", ErrorCodes.PermissionDenied);
        if (entity.IsShared || request.IsShared) await Policy().EnsureCanManageProjectAsync(projectId, cancellationToken);
        EnsureVersion(entity.VersionNo, request.VersionNo);
        if (request.IsDefault) await ClearDefaultAsync(projectId, request.IsShared, cancellationToken, entity.Id);
        entity.ViewName = request.ViewName.Trim(); entity.ViewKey = request.ViewKey; entity.QueryJson = queryJson; entity.IsShared = request.IsShared; entity.IsDefault = request.IsDefault; entity.VersionNo++; entity.UpdatedBy = User(); entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(string projectId, string id, long versionNo, CancellationToken cancellationToken = default)
    {
        await EnsureProjectAsync(projectId, cancellationToken);
        var db = databaseAccessor.GetCurrentDb();
        var entity = (await db.Queryable<ProjectManagementSavedViewEntity>().Where(item => item.Id == id && item.ProjectId == projectId && !item.IsDeleted).Take(1).ToListAsync(cancellationToken)).FirstOrDefault() ?? throw new NotFoundException("保存视图不存在", ErrorCodes.PlatformResourceNotFound);
        if (entity.IsShared) await Policy().EnsureCanManageProjectAsync(projectId, cancellationToken);
        else if (!string.Equals(entity.OwnerUserId, User(), StringComparison.OrdinalIgnoreCase)) throw new ValidationException("不能删除其他用户的视图", ErrorCodes.PermissionDenied);
        EnsureVersion(entity.VersionNo, versionNo); entity.IsDeleted = true; entity.DeletedBy = User(); entity.DeletedTime = DateTime.UtcNow; entity.UpdatedBy = User(); entity.UpdatedTime = entity.DeletedTime; entity.VersionNo++;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
    }

    private async Task ClearDefaultAsync(string projectId, bool shared, CancellationToken cancellationToken, string? excludedId = null)
    {
        var db = databaseAccessor.GetCurrentDb();
        var rows = await db.Queryable<ProjectManagementSavedViewEntity>().Where(item => item.ProjectId == projectId && item.IsShared == shared && item.IsDefault && !item.IsDeleted && (excludedId == null || item.Id != excludedId)).ToListAsync(cancellationToken);
        foreach (var row in rows) { row.IsDefault = false; row.VersionNo++; row.UpdatedBy = User(); row.UpdatedTime = DateTime.UtcNow; }
        if (rows.Count > 0) await db.Updateable(rows).ExecuteCommandAsync(cancellationToken);
    }

    private async Task EnsureProjectAsync(string projectId, CancellationToken cancellationToken) { Tenant(); App(); if (!await databaseAccessor.GetCurrentDb().Queryable<ProjectManagementProjectEntity>().Where(item => item.Id == projectId && !item.IsDeleted).AnyAsync(cancellationToken)) throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound); }
    private ProjectManagementAccessPolicy Policy() => accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser);
    private string Validate(ProjectManagementSavedViewUpsertRequest request) { if (string.IsNullOrWhiteSpace(request.ViewName) || request.ViewName.Trim().Length > 100) throw new ValidationException("视图名称不能为空且不能超过 100 个字符"); if (!ViewKeys.Contains(request.ViewKey, StringComparer.Ordinal)) throw new ValidationException("视图类型不受支持"); return ProjectManagementSavedViewState.Normalize(request.QueryJson, request.ViewKey, request.IsShared); }
    private string Tenant() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户", ErrorCodes.PermissionDenied);
    private string App() => currentUser.GetAsterErpAppCode()?.Trim().ToUpperInvariant() ?? throw new ValidationException("当前会话缺少应用", ErrorCodes.PermissionDenied);
    private string User() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户", ErrorCodes.PermissionDenied);
    private static void EnsureVersion(long current, long requested) { if (requested <= 0 || current != requested) throw new ValidationException("保存视图已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict); }
    private static ProjectManagementSavedViewResponse Map(ProjectManagementSavedViewEntity value) => new(value.Id, value.ProjectId, value.ViewName, value.ViewKey, value.QueryJson, value.OwnerUserId, value.IsShared, value.IsDefault, value.VersionNo, value.CreatedTime, value.UpdatedTime);
}
