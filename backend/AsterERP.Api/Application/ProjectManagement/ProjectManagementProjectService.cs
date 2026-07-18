using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Contracts.ProjectManagement;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Diagnostics;
using System.Text.Json;
using Volo.Abp.Users;

namespace AsterERP.Api.Application.ProjectManagement;

public sealed class ProjectManagementProjectService(
    IWorkspaceDatabaseAccessor databaseAccessor,
    ICurrentUser currentUser,
    ProjectManagementAccessPolicy? accessPolicy = null,
    IProjectManagementActivityWriter? activityWriter = null,
    IProjectManagementSyncJournalWriter? syncJournalWriter = null,
    IProjectManagementImConversationService? imConversationService = null) : IProjectManagementProjectService
{
    public async Task<GridPageResult<ProjectManagementProjectResponse>> QueryAsync(
        ProjectManagementProjectQuery query,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var db = databaseAccessor.GetProjectManagementDb();
        var keyword = NormalizeOptional(query.Keyword);
        var status = NormalizeOptional(query.Status);
        var ownerUserId = NormalizeOptional(query.OwnerUserId);
        var projectQuery = db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => !item.IsDeleted);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            projectQuery = projectQuery.Where(item =>
                item.ProjectCode.Contains(keyword) ||
                item.ProjectName.Contains(keyword) ||
                (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            projectQuery = projectQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(ownerUserId))
        {
            projectQuery = projectQuery.Where(item => item.OwnerUserId == ownerUserId);
        }

        var total = new RefAsync<int>();
        var items = await projectQuery
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);

        return new GridPageResult<ProjectManagementProjectResponse>
        {
            Total = total.Value,
            Items = items.Select(Map).ToList()
        };
    }

    public async Task<ProjectManagementProjectResponse> CreateAsync(
        ProjectManagementProjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var tenantId = RequireTenantId();
        var db = databaseAccessor.GetProjectManagementDb();
        ValidateRequest(request);
        var projectCode = NormalizeRequired(request.ProjectCode, "项目编码不能为空");
        if (await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item =>
                item.ProjectCode == projectCode && !item.IsDeleted, cancellationToken))
        {
            throw new ValidationException("项目编码已存在");
        }

        var now = DateTime.UtcNow;
        var entity = new ProjectManagementProjectEntity
        {
            TenantId = tenantId,
            AppCode = ProjectManagementPlatformScope.AppCode,
            ProjectCode = projectCode,
            ProjectName = NormalizeRequired(request.ProjectName, "项目名称不能为空"),
            Description = NormalizeOptional(request.Description),
            Status = ProjectManagementDomainRules.RequireProjectStatus(request.Status),
            Priority = NormalizePriority(request.Priority),
            OwnerUserId = NormalizeOptional(request.OwnerUserId) ?? RequireUserId(),
            StartDate = request.StartDate,
            DueDate = request.DueDate,
            WipLimit = request.WipLimit,
            ProgressPercent = NormalizeProgress(request.ProgressPercent),
            VersionNo = 1,
            CreatedBy = RequireUserId(),
            CreatedTime = now
        };
        var ownerMember = new ProjectManagementProjectMemberEntity
        {
            TenantId = tenantId,
            AppCode = ProjectManagementPlatformScope.AppCode,
            ProjectId = entity.Id,
            UserId = entity.OwnerUserId,
            RoleCode = "Owner",
            IsActive = true,
            JoinedAt = now,
            VersionNo = 1,
            CreatedBy = RequireUserId(),
            CreatedTime = now
        };
        db.Ado.BeginTran();
        try
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
            await db.Insertable(ownerMember).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "created", $"创建项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
        return Map(entity);
    }

    public async Task<ProjectManagementProjectResponse> UpdateAsync(
        string id,
        ProjectManagementProjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        if (entity.Status == ProjectManagementDomainRules.ProjectArchived)
            throw new ValidationException("项目已归档，只读不可编辑");
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageProjectAsync(id, cancellationToken);
        ValidateRequest(request);
        var projectCode = NormalizeRequired(request.ProjectCode, "项目编码不能为空");
        var db = databaseAccessor.GetProjectManagementDb();
        if (await db.Queryable<ProjectManagementProjectEntity>().AnyAsync(item =>
                item.ProjectCode == projectCode && item.Id != entity.Id && !item.IsDeleted, cancellationToken))
        {
            throw new ValidationException("项目编码已存在");
        }

        EnsureVersion(entity, request.VersionNo);
        entity.ProjectCode = projectCode;
        entity.ProjectName = NormalizeRequired(request.ProjectName, "项目名称不能为空");
        entity.Description = NormalizeOptional(request.Description);
        var nextStatus = ProjectManagementDomainRules.RequireProjectStatus(request.Status);
        ProjectManagementDomainRules.EnsureProjectStatusTransition(entity.Status, nextStatus);
        entity.Status = nextStatus;
        entity.Priority = NormalizePriority(request.Priority);
        entity.OwnerUserId = NormalizeOptional(request.OwnerUserId) ?? entity.OwnerUserId;
        entity.StartDate = request.StartDate;
        entity.DueDate = request.DueDate;
        entity.WipLimit = request.WipLimit;
        entity.ProgressPercent = NormalizeProgress(request.ProgressPercent);
        entity.CompletedAt = nextStatus == "Completed" ? entity.CompletedAt ?? DateTime.UtcNow : null;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeProjectLinksAsync(entity.Id, cancellationToken);
        }
        return Map(entity);
    }

    public async Task<ProjectManagementProjectResponse> ArchiveAsync(
        string id,
        ProjectManagementProjectArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageProjectAsync(id, cancellationToken);
        EnsureVersion(entity, request.VersionNo);
        ProjectManagementDomainRules.EnsureProjectStatusTransition(entity.Status, ProjectManagementDomainRules.ProjectArchived);
        entity.Status = ProjectManagementDomainRules.ProjectArchived;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetProjectManagementDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "archived", $"归档项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync(entity, "archived", cancellationToken);
        });
        if (imConversationService is not null)
            await imConversationService.ArchiveProjectLinksAsync(entity.Id, cancellationToken);
        return Map(entity);
    }

    public async Task<ProjectManagementProjectResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken, includeDeleted: true);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageDeletedProjectAsync(id, cancellationToken);
        EnsureVersion(entity, versionNo);
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetProjectManagementDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "restored", $"恢复项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync(entity, "restored", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateProjectLinksAsync(entity.Id, cancellationToken);
        }
        return Map(entity);
    }

    public async Task DeleteAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageProjectAsync(id, cancellationToken);
        EnsureVersion(entity, versionNo);
        entity.IsDeleted = true;
        entity.DeletedBy = RequireUserId();
        entity.DeletedTime = DateTime.UtcNow;
        entity.VersionNo++;
        entity.UpdatedBy = entity.DeletedBy;
        entity.UpdatedTime = entity.DeletedTime;
        var db = databaseAccessor.GetProjectManagementDb();
        if (imConversationService is not null)
        {
            await imConversationService.ArchiveProjectLinksAsync(entity.Id, cancellationToken);
        }
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
            await WriteActivityAsync(entity, "deleted", $"删除项目 {entity.ProjectName}", cancellationToken);
            await WriteSyncJournalAsync(entity, "deleted", cancellationToken);
        });
    }

    private async Task<ProjectManagementProjectEntity> GetRequiredAsync(string id, CancellationToken cancellationToken, bool includeDeleted = false)
    {
        RequirePlatformScope();
        var entity = await databaseAccessor.GetProjectManagementDb().Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == id && (includeDeleted || !item.IsDeleted))
            .Take(1)
            .ToListAsync(cancellationToken);
        return entity.FirstOrDefault() ?? throw new NotFoundException("项目不存在", ErrorCodes.PlatformResourceNotFound);
    }

    private string RequireTenantId() => currentUser.GetAsterErpTenantId()?.Trim() ?? throw new ValidationException("当前会话缺少租户");

    private string RequireUserId() => currentUser.GetAsterErpUserId()?.Trim() ?? throw new ValidationException("当前会话缺少用户");

    private void RequirePlatformScope() => ProjectManagementPlatformScope.RequireSystemWorkspace(currentUser);

    private async Task WriteActivityAsync(ProjectManagementProjectEntity entity, string activityType, string summary, CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(RequireTenantId(), ProjectManagementPlatformScope.AppCode, "Project", entity.Id, activityType, summary, Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.Id), cancellationToken);
    }

    private async Task WriteSyncJournalAsync(ProjectManagementProjectEntity entity, string operation, CancellationToken cancellationToken)
    {
        if (syncJournalWriter is null) return;
        await syncJournalWriter.AppendAsync(new ProjectManagementSyncJournalEvent(RequireTenantId(), ProjectManagementPlatformScope.AppCode, "Project", entity.Id, entity.Id, operation, entity.VersionNo, JsonSerializer.Serialize(entity), RequireUserId(), null, Activity.Current?.Id ?? Guid.NewGuid().ToString("N")), cancellationToken);
    }

    private static void ValidateRequest(ProjectManagementProjectUpsertRequest request)
    {
        if (request.WipLimit is < 0)
        {
            throw new ValidationException("WIP 上限不能为负数");
        }

        ProjectManagementDomainRules.ValidateDates(request.StartDate, request.DueDate, "项目");
    }

    private static void EnsureVersion(ProjectManagementProjectEntity entity, long versionNo)
    {
        if (versionNo <= 0 || versionNo != entity.VersionNo)
        {
            throw new ValidationException("项目已被其他用户修改，请刷新后重试", ErrorCodes.ApplicationDevelopmentPageRevisionConflict);
        }
    }

    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePriority(string value) => value.Trim() switch
    {
        "Low" or "Medium" or "High" or "Urgent" => value.Trim(),
        _ => throw new ValidationException("项目优先级不受支持")
    };

    private static decimal NormalizeProgress(decimal value) => value is < 0 or > 100 ? throw new ValidationException("项目进度必须在 0 到 100 之间") : value;

    private static ProjectManagementProjectResponse Map(ProjectManagementProjectEntity entity) => new(
        entity.Id, entity.TenantId, entity.AppCode, entity.ProjectCode, entity.ProjectName, entity.Description,
        entity.Status, entity.Priority, entity.OwnerUserId, entity.StartDate, entity.DueDate, entity.CompletedAt,
        entity.WipLimit, entity.ProgressPercent, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime);
}
