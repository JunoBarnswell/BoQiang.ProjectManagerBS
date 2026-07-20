using AsterERP.Api.Infrastructure.Database;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Modules.ProjectManagement;
using AsterERP.Api.Modules.System.Users;
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
    IProjectManagementImConversationService? imConversationService = null,
    IProjectManagementReversibleCommandWriter? reversibleCommandWriter = null,
    IProjectManagementDisplayProjectionService? displayProjection = null,
    IProjectManagementRealtimePublisher? realtimePublisher = null) : IProjectManagementProjectService
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
            var ownerUserIds = await DisplayProjection.FindUserIdsAsync(ownerUserId, cancellationToken);
            projectQuery = ownerUserIds.Count == 0
                ? projectQuery.Where(item => item.OwnerUserId == ownerUserId)
                : projectQuery.Where(item => item.OwnerUserId == ownerUserId || ownerUserIds.Contains(item.OwnerUserId));
        }

        var total = new RefAsync<int>();
        var items = await projectQuery
            .OrderBy(item => item.UpdatedTime, OrderByType.Desc)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 100), total, cancellationToken);

        return new GridPageResult<ProjectManagementProjectResponse>
        {
            Total = total.Value,
            Items = await MapManyAsync(items, cancellationToken)
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
        var ownerUserId = await ResolveOwnerUserIdAsync(request.OwnerUserId, cancellationToken);
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
            OwnerUserId = ownerUserId,
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
            await WriteActivityAsync(entity, "created", $"创建项目 {entity.ProjectName}", CreateChanges(null, entity), now, cancellationToken);
            await WriteSyncJournalAsync(entity, "created", cancellationToken);
            db.Ado.CommitTran();
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
        await PublishInvalidationAsync(entity, "project.created", ["projectCode", "projectName", "status", "priority", "ownerUserId", "startDate", "dueDate", "progressPercent"], request.ClientMutationId, null, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementProjectResponse> UpdateAsync(
        string id,
        ProjectManagementProjectUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        var beforeResponse = await MapAsync(entity, cancellationToken);
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

        var localValues = ProjectManagementProjectConflictLocalValues.FromUpdate(request);
        EnsureVersion(entity, request.VersionNo, localValues);
        var expectedVersion = entity.VersionNo;
        var before = ProjectActivitySnapshot.From(entity);
        entity.ProjectCode = projectCode;
        entity.ProjectName = NormalizeRequired(request.ProjectName, "项目名称不能为空");
        entity.Description = NormalizeOptional(request.Description);
        var nextStatus = ProjectManagementDomainRules.RequireProjectStatus(request.Status);
        ProjectManagementDomainRules.EnsureProjectStatusTransition(entity.Status, nextStatus);
        entity.Status = nextStatus;
        entity.Priority = NormalizePriority(request.Priority);
        var requestedOwnerUserId = NormalizeOptional(request.OwnerUserId);
        if (requestedOwnerUserId is not null)
            entity.OwnerUserId = await ResolveOwnerUserIdAsync(requestedOwnerUserId, cancellationToken);
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
            await UpdateWithExpectedVersionAsync(db, entity, expectedVersion, expectedDeleted: false, localValues, cancellationToken);
            if (!string.Equals(before.OwnerUserId, entity.OwnerUserId, StringComparison.Ordinal))
                await EnsureOwnerMembershipAsync(db, entity, before.OwnerUserId, cancellationToken);
            await WriteActivityAsync(entity, "updated", $"更新项目 {entity.ProjectName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "updated", cancellationToken);
        });
        var changedFields = CreateChanges(before, entity)
            .Where(change => !string.Equals(change.Field, "Description", StringComparison.Ordinal))
            .Select(change => ToRealtimeFieldName(change.Field))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var additionalHomeUsers = !string.Equals(before.OwnerUserId, entity.OwnerUserId, StringComparison.Ordinal)
            ? [before.OwnerUserId]
            : Array.Empty<string>();
        await PublishInvalidationAsync(entity, "project.updated", changedFields, request.ClientMutationId, additionalHomeUsers, cancellationToken);
        if (imConversationService is not null)
        {
            await imConversationService.SynchronizeProjectLinksAsync(entity.Id, cancellationToken);
        }
        var result = await MapAsync(entity, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.ProjectUpdated, entity.Id, "Project", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectUpdateCommand(entity.Id, request)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectUpdateCommand(entity.Id, ProjectManagementReversibleCommandHandler.ToUpsert(beforeResponse) with { VersionNo = result.VersionNo })),
            $"更新项目 {entity.ProjectName}", cancellationToken);
        return result;
    }

    public async Task<ProjectManagementProjectResponse> ArchiveAsync(
        string id,
        ProjectManagementProjectArchiveRequest request,
        CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageProjectAsync(id, cancellationToken);
        var localValues = ProjectManagementProjectConflictLocalValues.ForArchive(request.VersionNo);
        EnsureVersion(entity, request.VersionNo, localValues);
        var expectedVersion = entity.VersionNo;
        var before = ProjectActivitySnapshot.From(entity);
        ProjectManagementDomainRules.EnsureProjectStatusTransition(entity.Status, ProjectManagementDomainRules.ProjectArchived);
        entity.Status = ProjectManagementDomainRules.ProjectArchived;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetProjectManagementDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await UpdateWithExpectedVersionAsync(db, entity, expectedVersion, expectedDeleted: false, localValues, cancellationToken);
            await WriteActivityAsync(entity, "archived", $"归档项目 {entity.ProjectName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "archived", cancellationToken);
        });
        await PublishInvalidationAsync(entity, "project.archived", ["status", "updatedTime"], request.ClientMutationId, null, cancellationToken);
        if (imConversationService is not null)
            await imConversationService.ArchiveProjectLinksAsync(entity.Id, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ProjectManagementProjectResponse> RestoreAsync(string id, long versionNo, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken, includeDeleted: true);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageDeletedProjectAsync(id, cancellationToken);
        if (!entity.IsDeleted)
            throw new ValidationException("项目未删除，不能恢复");
        var localValues = ProjectManagementProjectConflictLocalValues.ForRestore(versionNo);
        EnsureVersion(entity, versionNo, localValues);
        var expectedVersion = entity.VersionNo;
        var before = ProjectActivitySnapshot.From(entity);
        entity.IsDeleted = false;
        entity.DeletedBy = null;
        entity.DeletedTime = null;
        entity.VersionNo++;
        entity.UpdatedBy = RequireUserId();
        entity.UpdatedTime = DateTime.UtcNow;
        var db = databaseAccessor.GetProjectManagementDb();
        await ProjectManagementMutationTransaction.RunAsync(db, async () =>
        {
            await UpdateWithExpectedVersionAsync(db, entity, expectedVersion, expectedDeleted: true, localValues, cancellationToken);
            await WriteActivityAsync(entity, "restored", $"恢复项目 {entity.ProjectName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "restored", cancellationToken);
        });
        if (imConversationService is not null)
        {
            await imConversationService.ReactivateProjectLinksAsync(entity.Id, cancellationToken);
        }
        var result = await MapAsync(entity, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.ProjectRestored, entity.Id, "Project", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectRestoreCommand(entity.Id, versionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectDeleteCommand(entity.Id, result.VersionNo)),
            $"恢复项目 {entity.ProjectName}", cancellationToken);
        return result;
    }

    public async Task DeleteAsync(string id, long versionNo, string? clientMutationId = null, CancellationToken cancellationToken = default)
    {
        RequirePlatformScope();
        var entity = await GetRequiredAsync(id, cancellationToken);
        await (accessPolicy ?? new ProjectManagementAccessPolicy(databaseAccessor, currentUser)).EnsureCanManageProjectAsync(id, cancellationToken);
        var localValues = ProjectManagementProjectConflictLocalValues.ForDelete(versionNo);
        EnsureVersion(entity, versionNo, localValues);
        var expectedVersion = entity.VersionNo;
        var before = ProjectActivitySnapshot.From(entity);
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
            await UpdateWithExpectedVersionAsync(db, entity, expectedVersion, expectedDeleted: false, localValues, cancellationToken);
            await WriteActivityAsync(entity, "deleted", $"删除项目 {entity.ProjectName}", CreateChanges(before, entity), entity.UpdatedTime ?? DateTime.UtcNow, cancellationToken);
            await WriteSyncJournalAsync(entity, "deleted", cancellationToken);
        });
        await PublishInvalidationAsync(entity, "project.deleted", ["isDeleted", "status", "updatedTime"], clientMutationId, null, cancellationToken);
        await RecordReversibleAsync(ProjectManagementReversibleCommandTypes.ProjectSoftDeleted, entity.Id, "Project", entity.Id,
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectDeleteCommand(entity.Id, versionNo)),
            ProjectManagementReversibleCommandHandler.Serialize(new ProjectManagementProjectRestoreCommand(entity.Id, entity.VersionNo)),
            $"删除项目 {entity.ProjectName}", cancellationToken);
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

    private async Task<string> ResolveOwnerUserIdAsync(string? requestedOwnerUserId, CancellationToken cancellationToken)
    {
        var ownerUserId = NormalizeOptional(requestedOwnerUserId) ?? RequireUserId();
        var currentDb = databaseAccessor.GetCurrentDb();
        if (!currentDb.DbMaintenance.IsAnyTable("system_users", false))
            return ownerUserId;

        var isAvailable = await currentDb.Queryable<SystemUserEntity>()
            .Where(user => user.Id == ownerUserId && !user.IsDeleted && user.Status == "Enabled")
            .AnyAsync(cancellationToken);
        if (!isAvailable)
            throw new ValidationException("负责人不存在、已删除或已停用");
        return ownerUserId;
    }

    private async Task EnsureOwnerMembershipAsync(
        ISqlSugarClient db,
        ProjectManagementProjectEntity project,
        string previousOwnerUserId,
        CancellationToken cancellationToken)
    {
        var now = project.UpdatedTime ?? DateTime.UtcNow;
        var members = await db.Queryable<ProjectManagementProjectMemberEntity>()
            .Where(member => member.ProjectId == project.Id && member.TenantId == project.TenantId && member.AppCode == project.AppCode &&
                (member.UserId == project.OwnerUserId || member.UserId == previousOwnerUserId))
            .ToListAsync(cancellationToken);
        var nextOwner = members.FirstOrDefault(member => member.UserId == project.OwnerUserId);
        if (nextOwner is null)
        {
            await db.Insertable(new ProjectManagementProjectMemberEntity
            {
                TenantId = project.TenantId,
                AppCode = project.AppCode,
                ProjectId = project.Id,
                UserId = project.OwnerUserId,
                RoleCode = "Owner",
                IsActive = true,
                JoinedAt = now,
                VersionNo = 1,
                CreatedBy = RequireUserId(),
                CreatedTime = now,
            }).ExecuteCommandAsync(cancellationToken);
        }
        else if (nextOwner.IsDeleted || !nextOwner.IsActive || !string.Equals(nextOwner.RoleCode, "Owner", StringComparison.Ordinal))
        {
            nextOwner.RoleCode = "Owner";
            nextOwner.ScopeRootTaskId = null;
            nextOwner.IsActive = true;
            nextOwner.LeftAt = null;
            nextOwner.IsDeleted = false;
            nextOwner.DeletedBy = null;
            nextOwner.DeletedTime = null;
            nextOwner.VersionNo = nextOwner.VersionNo <= 0 ? 1 : nextOwner.VersionNo + 1;
            nextOwner.UpdatedBy = RequireUserId();
            nextOwner.UpdatedTime = now;
            await db.Updateable(nextOwner).ExecuteCommandAsync(cancellationToken);
        }

        var previousOwner = members.FirstOrDefault(member => member.UserId == previousOwnerUserId && member.UserId != project.OwnerUserId &&
            member.IsActive && !member.IsDeleted && string.Equals(member.RoleCode, "Owner", StringComparison.Ordinal));
        if (previousOwner is null) return;
        previousOwner.RoleCode = "Member";
        previousOwner.VersionNo = previousOwner.VersionNo <= 0 ? 1 : previousOwner.VersionNo + 1;
        previousOwner.UpdatedBy = RequireUserId();
        previousOwner.UpdatedTime = now;
        await db.Updateable(previousOwner).ExecuteCommandAsync(cancellationToken);
    }

    private async Task WriteActivityAsync(
        ProjectManagementProjectEntity entity,
        string activityType,
        string summary,
        IReadOnlyList<ProjectManagementActivityFieldChange> changes,
        DateTime occurredAt,
        CancellationToken cancellationToken)
    {
        if (activityWriter is null) return;
        await activityWriter.AppendAsync(new ProjectManagementActivityEvent(
            RequireTenantId(), ProjectManagementPlatformScope.AppCode, "Project", entity.Id, activityType, summary,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"), RequireUserId(), entity.Id,
            Source: "User", FieldChanges: changes, OccurredAt: occurredAt), cancellationToken);
    }

    private Task RecordReversibleAsync(string commandType, string projectId, string aggregateType, string aggregateId, string forwardJson, string inverseJson, string summary, CancellationToken cancellationToken)
    {
        if (reversibleCommandWriter is null || ProjectManagementReversibleCommandReplayScope.IsActive) return Task.CompletedTask;
        var traceId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");
        return reversibleCommandWriter.TryRecordCommittedAsync(ProjectManagementReversibleCommandCapability.Instance,
            new ProjectManagementReversibleCommandRecordRequest(traceId, commandType, projectId, aggregateType, aggregateId, forwardJson, inverseJson, traceId, summary), cancellationToken);
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

    private static void EnsureVersion(
        ProjectManagementProjectEntity entity,
        long versionNo,
        ProjectManagementProjectConflictLocalValues localValues)
    {
        if (versionNo <= 0 || versionNo != entity.VersionNo)
        {
            throw CreateVersionConflictException(entity, localValues);
        }
    }

    private static async Task UpdateWithExpectedVersionAsync(
        ISqlSugarClient db,
        ProjectManagementProjectEntity entity,
        long expectedVersion,
        bool expectedDeleted,
        ProjectManagementProjectConflictLocalValues localValues,
        CancellationToken cancellationToken)
    {
        var affectedRows = await db.Updateable(entity)
            .Where(item => item.Id == entity.Id && item.VersionNo == expectedVersion && item.IsDeleted == expectedDeleted)
            .ExecuteCommandAsync(cancellationToken);
        if (affectedRows == 1)
            return;

        var server = (await db.Queryable<ProjectManagementProjectEntity>()
            .Where(item => item.Id == entity.Id)
            .Take(1)
            .ToListAsync(cancellationToken))
            .FirstOrDefault();
        if (server is null)
            throw new NotFoundException("项目已不存在", ErrorCodes.PlatformResourceNotFound);
        throw CreateVersionConflictException(server, localValues);
    }

    private static ProjectManagementProjectVersionConflictException CreateVersionConflictException(
        ProjectManagementProjectEntity entity,
        ProjectManagementProjectConflictLocalValues localValues) =>
        new(new ProjectManagementProjectVersionConflictResponse(
            Map(entity),
            localValues,
            CreateVersionConflictFields(entity, localValues)));

    private static IReadOnlyList<ProjectManagementProjectConflictField> CreateVersionConflictFields(
        ProjectManagementProjectEntity entity,
        ProjectManagementProjectConflictLocalValues localValues)
    {
        var fields = new List<ProjectManagementProjectConflictField>();
        foreach (var field in localValues.SubmittedFields)
        {
            var conflict = field switch
            {
                "VersionNo" => CreateVersionConflictField(field, "版本号", entity.VersionNo, localValues.VersionNo),
                "ProjectCode" => CreateVersionConflictField(field, "项目编码", entity.ProjectCode, localValues.ProjectCode),
                "ProjectName" => CreateVersionConflictField(field, "项目名称", entity.ProjectName, localValues.ProjectName),
                "Description" => CreateVersionConflictField(field, "项目描述", entity.Description, localValues.Description),
                "Status" => CreateVersionConflictField(field, "项目状态", entity.Status, localValues.Status),
                "Priority" => CreateVersionConflictField(field, "优先级", entity.Priority, localValues.Priority),
                "OwnerUserId" => CreateVersionConflictField(field, "负责人", entity.OwnerUserId, localValues.OwnerUserId),
                "StartDate" => CreateVersionConflictField(field, "开始日期", entity.StartDate, localValues.StartDate),
                "DueDate" => CreateVersionConflictField(field, "截止日期", entity.DueDate, localValues.DueDate),
                "WipLimit" => CreateVersionConflictField(field, "WIP 上限", entity.WipLimit, localValues.WipLimit),
                "ProgressPercent" => CreateVersionConflictField(field, "进度", entity.ProgressPercent, localValues.ProgressPercent),
                "IsDeleted" => CreateVersionConflictField(field, "已删除", entity.IsDeleted, localValues.IsDeleted),
                _ => null
            };
            if (conflict is not null)
                fields.Add(conflict);
        }
        return fields;
    }

    private static ProjectManagementProjectConflictField? CreateVersionConflictField(
        string field,
        string displayName,
        object? serverValue,
        object? localValue) =>
        Equals(serverValue, localValue) ? null : new ProjectManagementProjectConflictField(field, displayName, serverValue, localValue);

    private static string NormalizeRequired(string value, string message) => string.IsNullOrWhiteSpace(value) ? throw new ValidationException(message) : value.Trim();

    private async Task PublishInvalidationAsync(
        ProjectManagementProjectEntity entity,
        string eventType,
        IReadOnlyList<string> changedFields,
        string? clientMutationId,
        IReadOnlyCollection<string>? additionalHomeUserIds,
        CancellationToken cancellationToken)
    {
        if (realtimePublisher is null) return;
        await realtimePublisher.PublishInvalidationAsync(new ProjectManagementDataInvalidationEvent(
            RequireTenantId(),
            ProjectManagementPlatformScope.AppCode,
            "Project",
            entity.Id,
            eventType,
            entity.VersionNo,
            Activity.Current?.Id ?? Guid.NewGuid().ToString("N"),
            entity.Id,
            ChangedFields: changedFields,
            Patch: ToRealtimePatch(entity),
            ClientMutationId: clientMutationId,
            AdditionalHomeUserIds: additionalHomeUserIds), cancellationToken);
    }

    private static string ToRealtimeFieldName(string field) => field switch
    {
        "ProjectCode" => "projectCode",
        "ProjectName" => "projectName",
        "OwnerUserId" => "ownerUserId",
        "StartDate" => "startDate",
        "DueDate" => "dueDate",
        "WipLimit" => "wipLimit",
        "ProgressPercent" => "progressPercent",
        "IsDeleted" => "isDeleted",
        _ => char.ToLowerInvariant(field[0]) + field[1..]
    };

    private static IReadOnlyDictionary<string, object?> ToRealtimePatch(ProjectManagementProjectEntity entity) => new Dictionary<string, object?>
    {
        ["projectCode"] = entity.ProjectCode,
        ["projectName"] = entity.ProjectName,
        ["status"] = entity.Status,
        ["priority"] = entity.Priority,
        ["ownerUserId"] = entity.OwnerUserId,
        ["startDate"] = entity.StartDate,
        ["dueDate"] = entity.DueDate,
        ["wipLimit"] = entity.WipLimit,
        ["progressPercent"] = entity.ProgressPercent,
        ["isDeleted"] = entity.IsDeleted,
        ["versionNo"] = entity.VersionNo,
        ["updatedTime"] = entity.UpdatedTime
    };

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string NormalizePriority(string value) => value.Trim() switch
    {
        "Low" or "Medium" or "High" or "Urgent" => value.Trim(),
        _ => throw new ValidationException("项目优先级不受支持")
    };

    private static decimal NormalizeProgress(decimal value) => value is < 0 or > 100 ? throw new ValidationException("项目进度必须在 0 到 100 之间") : value;

    private static IReadOnlyList<ProjectManagementActivityFieldChange> CreateChanges(ProjectActivitySnapshot? before, ProjectManagementProjectEntity after) =>
        ProjectManagementActivityChanges.Collect(
            ProjectManagementActivityChanges.Create("ProjectCode", "项目编码", before?.ProjectCode, after.ProjectCode),
            ProjectManagementActivityChanges.Create("ProjectName", "项目名称", before?.ProjectName, after.ProjectName),
            ProjectManagementActivityChanges.Create("Description", "项目描述", before?.Description, after.Description),
            ProjectManagementActivityChanges.Create("Status", "项目状态", before?.Status, after.Status),
            ProjectManagementActivityChanges.Create("Priority", "优先级", before?.Priority, after.Priority),
            ProjectManagementActivityChanges.Create("OwnerUserId", "负责人", before?.OwnerUserId, after.OwnerUserId),
            ProjectManagementActivityChanges.Create("StartDate", "开始日期", before?.StartDate, after.StartDate),
            ProjectManagementActivityChanges.Create("DueDate", "截止日期", before?.DueDate, after.DueDate),
            ProjectManagementActivityChanges.Create("WipLimit", "WIP 上限", before?.WipLimit, after.WipLimit),
            ProjectManagementActivityChanges.Create("ProgressPercent", "进度", before?.ProgressPercent, after.ProgressPercent),
            ProjectManagementActivityChanges.Create("IsDeleted", "已删除", before?.IsDeleted, after.IsDeleted));

    private sealed record ProjectActivitySnapshot(
        string ProjectCode,
        string ProjectName,
        string? Description,
        string Status,
        string Priority,
        string OwnerUserId,
        DateTime? StartDate,
        DateTime? DueDate,
        int? WipLimit,
        decimal ProgressPercent,
        bool IsDeleted)
    {
        public static ProjectActivitySnapshot From(ProjectManagementProjectEntity entity) => new(
            entity.ProjectCode, entity.ProjectName, entity.Description, entity.Status, entity.Priority, entity.OwnerUserId,
            entity.StartDate, entity.DueDate, entity.WipLimit, entity.ProgressPercent, entity.IsDeleted);
    }

    private async Task<List<ProjectManagementProjectResponse>> MapManyAsync(IReadOnlyList<ProjectManagementProjectEntity> entities, CancellationToken cancellationToken)
    {
        var projection = await DisplayProjection.ResolveAsync([], [], entities.Select(item => item.OwnerUserId), cancellationToken);
        return entities.Select(item => Map(item, projection.User(item.OwnerUserId))).ToList();
    }

    private async Task<ProjectManagementProjectResponse> MapAsync(ProjectManagementProjectEntity entity, CancellationToken cancellationToken)
    {
        var projection = await DisplayProjection.ResolveAsync([], [], [entity.OwnerUserId], cancellationToken);
        return Map(entity, projection.User(entity.OwnerUserId));
    }

    private static ProjectManagementProjectResponse Map(ProjectManagementProjectEntity entity, string? ownerDisplayName = null) => new(
        entity.Id, entity.TenantId, entity.AppCode, entity.ProjectCode, entity.ProjectName, entity.Description,
        entity.Status, entity.Priority, entity.OwnerUserId, entity.StartDate, entity.DueDate, entity.CompletedAt,
        entity.WipLimit, entity.ProgressPercent, entity.VersionNo, entity.CreatedTime, entity.UpdatedTime, ownerDisplayName);

    private IProjectManagementDisplayProjectionService DisplayProjection => displayProjection ?? new ProjectManagementDisplayProjectionService(databaseAccessor);
}
