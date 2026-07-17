using System.Text.Json;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseManagementService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseManagementService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 500;

    public async Task<FlowiseOverviewDto> GetOverviewAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        var latest = await db.Queryable<FlowiseExecutionEntity>()
            .Where(item => !item.IsDeleted)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);

        return new FlowiseOverviewDto
        {
            AgentflowCount = await CountChatFlowAsync(FlowiseChatflowTypes.Agentflow, cancellationToken),
            ChatflowCount = await CountChatFlowAsync(FlowiseChatflowTypes.Chatflow, cancellationToken),
            DocumentStoreCount = await db.Queryable<FlowiseDocumentStoreEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            EvaluationCount = await db.Queryable<FlowiseEvaluationEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            ExecutionCount = await db.Queryable<FlowiseExecutionEntity>().CountAsync(item => !item.IsDeleted, cancellationToken),
            LatestExecution = latest is null ? null : FlowiseMapper.MapExecution(latest),
            WorkspaceCount = await db.Queryable<FlowiseWorkspaceEntity>().CountAsync(item => !item.IsDeleted, cancellationToken)
        };
    }

    public Task<IReadOnlyList<FlowiseResourceTypeDto>> GetResourceTypesAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        return Task.FromResult<IReadOnlyList<FlowiseResourceTypeDto>>(
        [
            ResourceType("tool", "tools", "Tools", PermissionCodes.FlowiseToolsView, PermissionCodes.FlowiseToolsEdit),
            ResourceType("credential", "credentials", "Credentials", PermissionCodes.FlowiseCredentialsView, PermissionCodes.FlowiseCredentialsEdit, supportsSecret: true),
            ResourceType("variable", "variables", "Variables", PermissionCodes.FlowiseVariablesView, PermissionCodes.FlowiseVariablesEdit, supportsSecret: true),
            ResourceType("api-key", "api-keys", "API Keys", PermissionCodes.FlowiseApiKeysView, PermissionCodes.FlowiseApiKeysEdit, supportsSecret: true),
            ResourceType("assistant", "assistants", "Assistants", PermissionCodes.FlowiseAssistantsView, PermissionCodes.FlowiseAssistantsEdit),
            ResourceType("marketplace", "marketplaces", "Marketplaces", PermissionCodes.FlowiseMarketplacesView, PermissionCodes.FlowiseMarketplacesEdit),
            ResourceType("document-store", "document-stores", "Document Stores", PermissionCodes.FlowiseDocumentStoresView, PermissionCodes.FlowiseDocumentStoresEdit),
            ResourceType("dataset", "datasets", "Datasets", PermissionCodes.FlowiseDatasetsView, PermissionCodes.FlowiseDatasetsEdit),
            ResourceType("evaluator", "evaluators", "Evaluators", PermissionCodes.FlowiseEvaluatorsView, PermissionCodes.FlowiseEvaluatorsEdit),
            ResourceType("evaluation", "evaluations", "Evaluations", PermissionCodes.FlowiseEvaluationsView, PermissionCodes.FlowiseEvaluationsEdit, supportsRun: true),
            ResourceType("sso-config", "sso-config", "SSO Config", PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseSsoManage, supportsSecret: true),
            ResourceType("role", "roles", "Roles", PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseRolesManage),
            ResourceType("user", "users", "Users", PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseUsersManage),
            ResourceType("login-activity", "login-activity", "Login Activity", PermissionCodes.FlowiseLoginActivityView, PermissionCodes.FlowiseLoginActivityManage),
            ResourceType("log", "logs", "Logs", PermissionCodes.FlowiseLogsView, PermissionCodes.FlowiseLogsManage),
            ResourceType("account", "account", "Account Settings", PermissionCodes.FlowiseAccountView, PermissionCodes.FlowiseAccountEdit)
        ]);
    }

    public async Task<GridPageResult<FlowiseWorkspaceDto>> GetWorkspacesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWorkspacesView, PermissionCodes.FlowiseWorkspacesManage, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseWorkspaceEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceKey.Contains(keyword) || item.WorkspaceName.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseWorkspaceDto>
        {
            Items = rows.Select(FlowiseMapper.MapWorkspace).ToList(),
            Total = total.Value
        };
    }

    public async Task<FlowiseWorkspaceDto> UpsertWorkspaceAsync(string? id, FlowiseWorkspaceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWorkspacesManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeWorkspaceRequest(request);
        var workspace = workspaceContext.Resolve();
        var existing = string.IsNullOrWhiteSpace(id)
            ? await db.Queryable<FlowiseWorkspaceEntity>().FirstAsync(item => !item.IsDeleted && item.WorkspaceKey == normalized.WorkspaceKey, cancellationToken)
            : await db.Queryable<FlowiseWorkspaceEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken);
        if (existing is not null && !string.IsNullOrWhiteSpace(id))
        {
            var duplicate = await db.Queryable<FlowiseWorkspaceEntity>()
                .AnyAsync(item => !item.IsDeleted && item.Id != existing.Id && item.WorkspaceKey == normalized.WorkspaceKey, cancellationToken);
            if (duplicate)
            {
                throw new ValidationException("Flowise 工作区编码已存在", ErrorCodes.ParameterInvalid);
            }
        }

        var entity = existing ?? new FlowiseWorkspaceEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        entity.WorkspaceKey = normalized.WorkspaceKey;
        entity.WorkspaceName = normalized.WorkspaceName;
        entity.Status = normalized.Status ?? "Enabled";
        entity.Description = normalized.Description;
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        await WriteAuditAsync("workspace.saved", "workspace", entity.Id, entity.WorkspaceKey, cancellationToken);
        return FlowiseMapper.MapWorkspace(entity);
    }

    public async Task<bool> DeleteWorkspaceAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWorkspacesManage, PermissionCodes.FlowiseManage);
        var entity = await db.Queryable<FlowiseWorkspaceEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise 工作区不存在", ErrorCodes.ParameterInvalid);
        if (await WorkspaceHasReferencesAsync(entity.Id, cancellationToken))
        {
            throw new ValidationException("工作区存在关联 Flowise 资源，不能直接删除", ErrorCodes.ParameterInvalid);
        }

        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("workspace.deleted", "workspace", entity.Id, entity.WorkspaceKey, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FlowiseSharedWorkspaceDto>> GetSharedWorkspacesAsync(string itemId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWorkspacesView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var target = await LoadShareTargetAsync(itemId, null, cancellationToken);
        var workspaces = await db.Queryable<FlowiseWorkspaceEntity>()
            .Where(item => !item.IsDeleted && item.Id != target.WorkspaceId)
            .OrderBy(item => item.WorkspaceName)
            .ToListAsync(cancellationToken);
        var sharedIds = await db.Queryable<FlowiseSharedWorkspaceEntity>()
            .Where(item => !item.IsDeleted && item.ItemId == target.ItemId && item.ItemType == target.ItemType)
            .Select(item => item.SharedWorkspaceId)
            .ToListAsync(cancellationToken);
        var sharedSet = sharedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return workspaces.Select(item => new FlowiseSharedWorkspaceDto
        {
            Shared = sharedSet.Contains(item.Id),
            WorkspaceId = item.Id,
            WorkspaceName = item.WorkspaceName
        }).ToList();
    }

    public async Task<IReadOnlyList<FlowiseSharedWorkspaceDto>> SetSharedWorkspacesAsync(string itemId, FlowiseShareWorkspacesRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWorkspacesManage, PermissionCodes.FlowiseManage);
        var target = await LoadShareTargetAsync(itemId, NormalizeOptional(request.ItemType), cancellationToken);
        var workspaceIds = request.WorkspaceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(id => !string.Equals(id, target.WorkspaceId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (workspaceIds.Count > 0)
        {
            var existingCount = await db.Queryable<FlowiseWorkspaceEntity>()
                .CountAsync(item => !item.IsDeleted && workspaceIds.Contains(item.Id), cancellationToken);
            if (existingCount != workspaceIds.Count)
            {
                throw new ValidationException("Flowise 共享工作区不存在", ErrorCodes.ParameterInvalid);
            }
        }

        var workspace = workspaceContext.Resolve();
        await db.Ado.UseTranAsync(async () =>
        {
            await db.Updateable<FlowiseSharedWorkspaceEntity>()
                .SetColumns(item => new FlowiseSharedWorkspaceEntity
                {
                    IsDeleted = true,
                    DeletedTime = DateTime.UtcNow
                })
                .Where(item => !item.IsDeleted && item.ItemId == target.ItemId && item.ItemType == target.ItemType)
                .ExecuteCommandAsync(cancellationToken);
            if (workspaceIds.Count == 0)
            {
                return;
            }

            var rows = workspaceIds.Select(sharedWorkspaceId => new FlowiseSharedWorkspaceEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                WorkspaceId = target.WorkspaceId,
                ItemId = target.ItemId,
                ItemType = target.ItemType,
                SharedWorkspaceId = sharedWorkspaceId
            }).ToList();
            await db.Insertable(rows).ExecuteCommandAsync(cancellationToken);
        });
        await WriteAuditAsync("shared-workspaces.saved", target.ItemType, target.ItemId, string.Join(",", workspaceIds), cancellationToken);
        return await GetSharedWorkspacesAsync(itemId, cancellationToken);
    }

    public async Task<GridPageResult<FlowiseResourceDto>> GetSsoConfigsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseSsoConfigEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ConfigKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseResourceDto> { Items = rows.Select(Map).ToList(), Total = total.Value };
    }

    public async Task<FlowiseResourceDto> CreateSsoConfigAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeResourceRequest(request, "SSO Config");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseSsoConfigEntity>().AnyAsync(item => !item.IsDeleted && item.ConfigKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("SSO Config 编码已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseSsoConfigEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("sso-config.created", "sso-config", entity.Id, entity.ConfigKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateSsoConfigAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseManage);
        var entity = await LoadSsoAsync(id, cancellationToken);
        var normalized = NormalizeResourceRequest(request, "SSO Config");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseSsoConfigEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.ConfigKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("SSO Config 编码已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("sso-config.updated", "sso-config", entity.Id, entity.ConfigKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteSsoConfigAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseManage);
        var entity = await LoadSsoAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("sso-config.deleted", "sso-config", entity.Id, entity.ConfigKey, cancellationToken);
        return true;
    }

    public async Task<FlowiseSsoConfigDto?> GetSsoConfigDetailAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseSsoManage, PermissionCodes.FlowiseView);
        var row = await db.Queryable<FlowiseSsoConfigEntity>().Where(item => !item.IsDeleted).OrderBy(item => item.CreatedTime, OrderByType.Desc).FirstAsync(cancellationToken);
        return row is null ? null : new FlowiseSsoConfigDto { Enabled = row.Enabled, Id = row.Id, Provider = row.Provider, SettingsJson = row.SettingsJson };
    }

    public Task<GridPageResult<FlowiseResourceDto>> GetRolesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        GetRolePageAsync(query, cancellationToken);

    public async Task<FlowiseResourceDto> CreateRoleAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeResourceRequest(request, "Role");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseRoleEntity>().AnyAsync(item => !item.IsDeleted && item.RoleKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Role 编码已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseRoleEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("role.created", "role", entity.Id, entity.RoleKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateRoleAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseManage);
        var entity = await LoadRoleAsync(id, cancellationToken);
        var normalized = NormalizeResourceRequest(request, "Role");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseRoleEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.RoleKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Role 编码已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("role.updated", "role", entity.Id, entity.RoleKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteRoleAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseManage);
        var entity = await LoadRoleAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("role.deleted", "role", entity.Id, entity.RoleKey, cancellationToken);
        return true;
    }

    public async Task<FlowiseRoleDto> GetRoleDetailAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseView);
        var entity = await LoadRoleAsync(id, cancellationToken);
        return new FlowiseRoleDto
        {
            Description = entity.Description,
            Id = entity.Id,
            Name = entity.Name,
            Permissions = ReadStringArray(entity.PermissionsJson),
            Status = entity.Status
        };
    }

    public Task<GridPageResult<FlowiseResourceDto>> GetUsersAsync(FlowiseStudioQuery query, CancellationToken cancellationToken) =>
        GetUserPageAsync(query, cancellationToken);

    public async Task<FlowiseResourceDto> CreateUserAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeResourceRequest(request, "User");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseUserEntity>().AnyAsync(item => !item.IsDeleted && item.UserKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("User 编码已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseUserEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("user.created", "user", entity.Id, entity.UserKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateUserAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseManage);
        var entity = await LoadUserAsync(id, cancellationToken);
        var normalized = NormalizeResourceRequest(request, "User");
        await EnsureWorkspaceExistsAsync(normalized.WorkspaceId, cancellationToken);
        var duplicate = await db.Queryable<FlowiseUserEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.UserKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("User 编码已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("user.updated", "user", entity.Id, entity.UserKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteUserAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseManage);
        var entity = await LoadUserAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("user.deleted", "user", entity.Id, entity.UserKey, cancellationToken);
        return true;
    }

    public async Task<FlowiseUserDto> GetUserDetailAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseView);
        var entity = await LoadUserAsync(id, cancellationToken);
        return new FlowiseUserDto
        {
            Email = entity.Email,
            Id = entity.Id,
            Name = entity.Name,
            Roles = ReadStringArray(entity.RolesJson),
            Status = entity.Status,
            WorkspaceIds = ReadStringArray(entity.WorkspaceIdsJson)
        };
    }

    public async Task<GridPageResult<FlowiseResourceDto>> GetLoginActivityResourcesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLoginActivityView, PermissionCodes.FlowiseLoginActivityManage, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseLoginActivityEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.UserName.Contains(keyword) || (item.IpAddress != null && item.IpAddress.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseResourceDto> { Items = rows.Select(Map).ToList(), Total = total.Value };
    }

    public async Task<FlowiseResourceDto> CreateLoginActivityAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLoginActivityManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeResourceRequest(request, "Login Activity");
        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseLoginActivityEntity { TenantId = workspace.TenantId, AppCode = workspace.AppCode, OwnerUserId = workspace.UserId };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("login-activity.created", "login-activity", entity.Id, entity.UserName, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateLoginActivityAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLoginActivityManage, PermissionCodes.FlowiseManage);
        var entity = await LoadLoginActivityAsync(id, cancellationToken);
        var normalized = NormalizeResourceRequest(request, "Login Activity");
        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("login-activity.updated", "login-activity", entity.Id, entity.UserName, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteLoginActivityAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLoginActivityManage, PermissionCodes.FlowiseManage);
        var entity = await LoadLoginActivityAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<GridPageResult<FlowiseLoginActivityDto>> GetLoginActivityAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLoginActivityView, PermissionCodes.FlowiseLoginActivityManage, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseLoginActivityEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.UserName.Contains(keyword) || (item.IpAddress != null && item.IpAddress.Contains(keyword)));
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseLoginActivityDto>
        {
            Items = rows.Select(item => new FlowiseLoginActivityDto
            {
                CreatedTime = item.CreatedTime,
                Id = item.Id,
                IpAddress = item.IpAddress,
                Status = item.Status,
                UserAgent = item.UserAgent,
                UserName = item.UserName
            }).ToList(),
            Total = total.Value
        };
    }

    public async Task<GridPageResult<FlowiseResourceDto>> GetLogResourcesAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLogsView, PermissionCodes.FlowiseLogsRead, PermissionCodes.FlowiseView);
        var dbQuery = FilterLogs(query);
        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseResourceDto> { Items = rows.Select(Map).ToList(), Total = total.Value };
    }

    public async Task<FlowiseResourceDto> CreateLogAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLogsManage, PermissionCodes.FlowiseManage);
        var normalized = NormalizeResourceRequest(request, "Log");
        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            EventType = normalized.ResourceKey,
            ResourceType = normalized.Category ?? "manual",
            DetailJson = normalized.DefinitionJson ?? "{}",
            WorkspaceId = normalized.WorkspaceId
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateLogAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLogsManage, PermissionCodes.FlowiseManage);
        var entity = await LoadLogAsync(id, cancellationToken);
        var normalized = NormalizeResourceRequest(request, "Log");
        entity.EventType = normalized.ResourceKey;
        entity.ResourceType = normalized.Category ?? "manual";
        entity.DetailJson = normalized.DefinitionJson ?? "{}";
        entity.WorkspaceId = normalized.WorkspaceId;
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteLogAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLogsManage, PermissionCodes.FlowiseManage);
        var entity = await LoadLogAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    public async Task<GridPageResult<FlowiseAuditLogDto>> GetLogsAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseLogsView, PermissionCodes.FlowiseLogsRead, PermissionCodes.FlowiseView);
        var dbQuery = FilterLogs(query);
        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseAuditLogDto>
        {
            Items = rows.Select(item => new FlowiseAuditLogDto
            {
                CreatedTime = item.CreatedTime,
                DetailJson = item.DetailJson,
                EventType = item.EventType,
                Id = item.Id,
                ResourceId = item.ResourceId,
                ResourceType = item.ResourceType
            }).ToList(),
            Total = total.Value
        };
    }

    public async Task<FlowiseAccountSettingsDto> GetAccountAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAccountView, PermissionCodes.FlowiseView);
        var workspace = workspaceContext.Resolve();
        var entity = await db.Queryable<FlowiseAccountSettingEntity>().FirstAsync(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId, cancellationToken);
        return entity is null
            ? new FlowiseAccountSettingsDto { DisplayName = workspace.UserId, PreferencesJson = "{}" }
            : new FlowiseAccountSettingsDto { DisplayName = entity.DisplayName, Email = entity.Email, PreferencesJson = entity.PreferencesJson };
    }

    public async Task<FlowiseAccountSettingsDto> UpdateAccountAsync(FlowiseAccountSettingsDto request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAccountEdit, PermissionCodes.FlowiseEdit);
        var workspace = workspaceContext.Resolve();
        var preferencesJson = FlowiseResourceJson.NormalizeObject(request.PreferencesJson, "PreferencesJson");
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName) ? workspace.UserId : request.DisplayName.Trim();
        var entity = await db.Queryable<FlowiseAccountSettingEntity>().FirstAsync(item => !item.IsDeleted && item.OwnerUserId == workspace.UserId, cancellationToken);
        if (entity is null)
        {
            entity = new FlowiseAccountSettingEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                DisplayName = displayName,
                Email = NormalizeOptional(request.Email),
                PreferencesJson = preferencesJson
            };
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            entity.DisplayName = displayName;
            entity.Email = NormalizeOptional(request.Email);
            entity.PreferencesJson = preferencesJson;
            entity.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        await WriteAuditAsync("account.updated", "account", entity.Id, displayName, cancellationToken);
        return new FlowiseAccountSettingsDto { DisplayName = entity.DisplayName, Email = entity.Email, PreferencesJson = entity.PreferencesJson };
    }

    private async Task<GridPageResult<FlowiseResourceDto>> GetRolePageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseRolesManage, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseRoleEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.RoleKey.Contains(keyword) || item.Name.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseResourceDto> { Items = rows.Select(Map).ToList(), Total = total.Value };
    }

    private async Task<GridPageResult<FlowiseResourceDto>> GetUserPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseUsersManage, PermissionCodes.FlowiseView);
        var dbQuery = db.Queryable<FlowiseUserEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.UserKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Email != null && item.Email.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.WorkspaceId))
        {
            var workspaceId = query.WorkspaceId.Trim();
            dbQuery = dbQuery.Where(item => item.WorkspaceId == workspaceId);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc).ToPageListAsync(PageIndex(query), PageSize(query), total);
        return new GridPageResult<FlowiseResourceDto> { Items = rows.Select(Map).ToList(), Total = total.Value };
    }

    private async Task<bool> WorkspaceHasReferencesAsync(string workspaceId, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseChatFlowEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseToolEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseCredentialEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseVariableEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseApiKeyEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseAssistantEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseMarketplaceTemplateEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseDocumentStoreEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseDatasetEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseEvaluatorEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseEvaluationEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseSsoConfigEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseRoleEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken)
            || await db.Queryable<FlowiseUserEntity>().AnyAsync(item => !item.IsDeleted && item.WorkspaceId == workspaceId, cancellationToken);
    }

    private async Task<FlowiseShareTarget> LoadShareTargetAsync(string itemId, string? itemType, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            throw new ValidationException("缺少 Flowise 共享对象 Id", ErrorCodes.ParameterInvalid);
        }

        var normalizedId = itemId.Trim();
        var target = await TryLoadShareTargetAsync(normalizedId, cancellationToken);
        if (target is null)
        {
            throw new ValidationException("Flowise 共享对象不存在", ErrorCodes.ParameterInvalid);
        }

        if (!string.IsNullOrWhiteSpace(itemType) &&
            !string.Equals(NormalizeSharedItemType(itemType), target.ItemType, StringComparison.Ordinal))
        {
            throw new ValidationException("Flowise 共享对象类型不匹配", ErrorCodes.ParameterInvalid);
        }

        return target;
    }

    private async Task<FlowiseShareTarget?> TryLoadShareTargetAsync(string id, CancellationToken cancellationToken)
    {
        var chatflow = await db.Queryable<FlowiseChatFlowEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (chatflow is not null) return new FlowiseShareTarget(chatflow.Id, NormalizeChatflowItemType(chatflow.Type), chatflow.WorkspaceId);
        var tool = await db.Queryable<FlowiseToolEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (tool is not null) return new FlowiseShareTarget(tool.Id, FlowiseSharedItemTypes.Tool, tool.WorkspaceId);
        var credential = await db.Queryable<FlowiseCredentialEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (credential is not null) return new FlowiseShareTarget(credential.Id, FlowiseSharedItemTypes.Credential, credential.WorkspaceId);
        var variable = await db.Queryable<FlowiseVariableEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (variable is not null) return new FlowiseShareTarget(variable.Id, FlowiseSharedItemTypes.Variable, variable.WorkspaceId);
        var apiKey = await db.Queryable<FlowiseApiKeyEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (apiKey is not null) return new FlowiseShareTarget(apiKey.Id, FlowiseSharedItemTypes.ApiKey, apiKey.WorkspaceId);
        var assistant = await db.Queryable<FlowiseAssistantEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (assistant is not null) return new FlowiseShareTarget(assistant.Id, FlowiseSharedItemTypes.Assistant, assistant.WorkspaceId);
        var marketplace = await db.Queryable<FlowiseMarketplaceTemplateEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (marketplace is not null) return new FlowiseShareTarget(marketplace.Id, FlowiseSharedItemTypes.Marketplace, marketplace.WorkspaceId);
        var store = await db.Queryable<FlowiseDocumentStoreEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (store is not null) return new FlowiseShareTarget(store.Id, FlowiseSharedItemTypes.DocumentStore, store.WorkspaceId);
        var dataset = await db.Queryable<FlowiseDatasetEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (dataset is not null) return new FlowiseShareTarget(dataset.Id, FlowiseSharedItemTypes.Dataset, dataset.WorkspaceId);
        var evaluator = await db.Queryable<FlowiseEvaluatorEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (evaluator is not null) return new FlowiseShareTarget(evaluator.Id, FlowiseSharedItemTypes.Evaluator, evaluator.WorkspaceId);
        var evaluation = await db.Queryable<FlowiseEvaluationEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (evaluation is not null) return new FlowiseShareTarget(evaluation.Id, FlowiseSharedItemTypes.Evaluation, evaluation.WorkspaceId);
        var sso = await db.Queryable<FlowiseSsoConfigEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (sso is not null) return new FlowiseShareTarget(sso.Id, FlowiseSharedItemTypes.SsoConfig, sso.WorkspaceId);
        var role = await db.Queryable<FlowiseRoleEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        if (role is not null) return new FlowiseShareTarget(role.Id, FlowiseSharedItemTypes.Role, role.WorkspaceId);
        var user = await db.Queryable<FlowiseUserEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id, cancellationToken);
        return user is null ? null : new FlowiseShareTarget(user.Id, FlowiseSharedItemTypes.User, user.WorkspaceId);
    }

    private async Task EnsureWorkspaceExistsAsync(string? workspaceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
        {
            return;
        }

        var exists = await db.Queryable<FlowiseWorkspaceEntity>().AnyAsync(item => !item.IsDeleted && item.Id == workspaceId, cancellationToken);
        if (!exists)
        {
            throw new ValidationException("Flowise 工作区不存在", ErrorCodes.ParameterInvalid);
        }
    }

    private async Task WriteAuditAsync(string eventType, string resourceType, string? resourceId, string detail, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            EventType = eventType,
            ResourceType = resourceType,
            ResourceId = resourceId,
            DetailJson = JsonSerializer.Serialize(new { detail })
        }).ExecuteCommandAsync(cancellationToken);
    }

    private ISugarQueryable<FlowiseAuditLogEntity> FilterLogs(FlowiseStudioQuery query)
    {
        var dbQuery = db.Queryable<FlowiseAuditLogEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.EventType.Contains(keyword) || item.ResourceType.Contains(keyword) || item.DetailJson.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            dbQuery = dbQuery.Where(item => item.ResourceType == category);
        }

        return dbQuery;
    }

    private static FlowiseResourceTypeDto ResourceType(string type, string route, string name, string view, string edit, bool supportsSecret = false, bool supportsRun = false) => new()
    {
        DisplayName = name,
        EditPermission = edit,
        ResourceType = type,
        RouteSegment = route,
        SupportsCanvas = false,
        SupportsRun = supportsRun,
        SupportsSecret = supportsSecret,
        ViewPermission = view
    };

    private static FlowiseResourceUpsertRequest NormalizeResourceRequest(FlowiseResourceUpsertRequest request, string name)
    {
        return new FlowiseResourceUpsertRequest
        {
            Category = NormalizeOptional(request.Category),
            DefinitionJson = FlowiseResourceJson.NormalizeObject(request.DefinitionJson, "DefinitionJson"),
            Description = NormalizeOptional(request.Description),
            DisplayName = FlowiseResourceJson.Required(request.DisplayName, $"{name} 名称"),
            MetadataJson = FlowiseResourceJson.NormalizeObject(request.MetadataJson, "MetadataJson"),
            ResourceKey = FlowiseResourceJson.Required(request.ResourceKey, $"{name} 编码").ToLowerInvariant(),
            SecretValue = NormalizeOptional(request.SecretValue),
            Status = FlowiseResourceJson.NormalizeStatus(request.Status),
            WorkspaceId = NormalizeOptional(request.WorkspaceId)
        };
    }

    private static FlowiseWorkspaceUpsertRequest NormalizeWorkspaceRequest(FlowiseWorkspaceUpsertRequest request)
    {
        return new FlowiseWorkspaceUpsertRequest
        {
            Description = NormalizeOptional(request.Description),
            Status = FlowiseResourceJson.NormalizeStatus(request.Status),
            WorkspaceKey = FlowiseResourceJson.Required(request.WorkspaceKey, "Flowise 工作区编码").ToLowerInvariant(),
            WorkspaceName = FlowiseResourceJson.Required(request.WorkspaceName, "Flowise 工作区名称")
        };
    }

    private static IReadOnlyList<string> ReadStringArray(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string StringArrayJson(IReadOnlyList<string> values) => JsonSerializer.Serialize(values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));

    private static JsonElement ReadObject(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<string> ReadStringArrayFromMetadata(string metadataJson, string property)
    {
        var root = ReadObject(metadataJson);
        if (!root.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? string.Empty)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static bool ReadBoolFromDefinition(string definitionJson, string property)
    {
        var root = ReadObject(definitionJson);
        return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
    }

    private static string ReadStringFromDefinition(string definitionJson, string property, string fallback)
    {
        var root = ReadObject(definitionJson);
        return root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int PageIndex(FlowiseStudioQuery query) => Math.Max(query.PageIndex, 1);

    private static int PageSize(FlowiseStudioQuery query) => Math.Clamp(query.PageSize <= 0 ? DefaultPageSize : query.PageSize, 1, MaxPageSize);

    private static FlowiseResourceDto Map(FlowiseSsoConfigEntity entity) => new()
    {
        Category = entity.Provider,
        CreatedTime = entity.CreatedTime,
        DefinitionJson = entity.SettingsJson,
        Description = entity.Description,
        DisplayName = entity.Name,
        Id = entity.Id,
        MetadataJson = entity.MetadataJson,
        ResourceKey = entity.ConfigKey,
        ResourceType = "sso-config",
        Status = entity.Status,
        UpdatedTime = entity.UpdatedTime,
        WorkspaceId = entity.WorkspaceId
    };

    private static FlowiseResourceDto Map(FlowiseRoleEntity entity) => new()
    {
        Category = "Role",
        CreatedTime = entity.CreatedTime,
        DefinitionJson = JsonSerializer.Serialize(new { permissions = ReadStringArray(entity.PermissionsJson) }),
        Description = entity.Description,
        DisplayName = entity.Name,
        Id = entity.Id,
        MetadataJson = entity.MetadataJson,
        ResourceKey = entity.RoleKey,
        ResourceType = "role",
        Status = entity.Status,
        UpdatedTime = entity.UpdatedTime,
        WorkspaceId = entity.WorkspaceId
    };

    private static FlowiseResourceDto Map(FlowiseUserEntity entity) => new()
    {
        Category = "User",
        CreatedTime = entity.CreatedTime,
        DefinitionJson = JsonSerializer.Serialize(new { email = entity.Email, roles = ReadStringArray(entity.RolesJson), workspaceIds = ReadStringArray(entity.WorkspaceIdsJson) }),
        Description = entity.Description,
        DisplayName = entity.Name,
        Id = entity.Id,
        MetadataJson = entity.MetadataJson,
        ResourceKey = entity.UserKey,
        ResourceType = "user",
        Status = entity.Status,
        UpdatedTime = entity.UpdatedTime,
        WorkspaceId = entity.WorkspaceId
    };

    private static FlowiseResourceDto Map(FlowiseLoginActivityEntity entity) => new()
    {
        Category = entity.Status,
        CreatedTime = entity.CreatedTime,
        DefinitionJson = entity.DetailJson,
        Description = entity.IpAddress,
        DisplayName = entity.UserName,
        Id = entity.Id,
        MetadataJson = JsonSerializer.Serialize(new { entity.IpAddress, entity.UserAgent }),
        ResourceKey = entity.Id,
        ResourceType = "login-activity",
        Status = entity.Status,
        UpdatedTime = entity.UpdatedTime,
        WorkspaceId = entity.WorkspaceId
    };

    private static FlowiseResourceDto Map(FlowiseAuditLogEntity entity) => new()
    {
        Category = entity.ResourceType,
        CreatedTime = entity.CreatedTime,
        DefinitionJson = entity.DetailJson,
        DisplayName = entity.EventType,
        Id = entity.Id,
        MetadataJson = JsonSerializer.Serialize(new { entity.ResourceId }),
        ResourceKey = entity.EventType,
        ResourceType = "log",
        Status = "Enabled",
        UpdatedTime = entity.UpdatedTime,
        WorkspaceId = entity.WorkspaceId
    };

    private static void Apply(FlowiseSsoConfigEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.ConfigKey = request.ResourceKey;
        entity.Description = request.Description;
        entity.Enabled = string.Equals(request.Status, "Enabled", StringComparison.OrdinalIgnoreCase);
        entity.MetadataJson = request.MetadataJson ?? "{}";
        entity.Name = request.DisplayName;
        entity.Provider = request.Category ?? ReadStringFromDefinition(request.DefinitionJson ?? "{}", "provider", "sso");
        entity.SettingsJson = request.DefinitionJson ?? "{}";
        entity.Status = request.Status ?? "Enabled";
        entity.WorkspaceId = request.WorkspaceId;
    }

    private static void Apply(FlowiseRoleEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.Description = request.Description;
        entity.MetadataJson = request.MetadataJson ?? "{}";
        entity.Name = request.DisplayName;
        entity.PermissionsJson = StringArrayJson(ReadStringArrayFromMetadata(request.DefinitionJson ?? "{}", "permissions"));
        entity.RoleKey = request.ResourceKey;
        entity.Status = request.Status ?? "Enabled";
        entity.WorkspaceId = request.WorkspaceId;
    }

    private static void Apply(FlowiseUserEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.Description = request.Description;
        entity.Email = ReadStringFromDefinition(request.DefinitionJson ?? "{}", "email", string.Empty);
        entity.MetadataJson = request.MetadataJson ?? "{}";
        entity.Name = request.DisplayName;
        entity.RolesJson = StringArrayJson(ReadStringArrayFromMetadata(request.DefinitionJson ?? "{}", "roles"));
        entity.Status = request.Status ?? "Enabled";
        entity.UserKey = request.ResourceKey;
        entity.WorkspaceId = request.WorkspaceId;
        entity.WorkspaceIdsJson = StringArrayJson(ReadStringArrayFromMetadata(request.DefinitionJson ?? "{}", "workspaceIds"));
    }

    private static void Apply(FlowiseLoginActivityEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.DetailJson = request.DefinitionJson ?? "{}";
        entity.IpAddress = ReadStringFromDefinition(request.MetadataJson ?? "{}", "ipAddress", string.Empty);
        entity.Status = request.Status ?? "Success";
        entity.UserAgent = ReadStringFromDefinition(request.MetadataJson ?? "{}", "userAgent", string.Empty);
        entity.UserName = request.DisplayName;
        entity.WorkspaceId = request.WorkspaceId;
    }

    private async Task<FlowiseSsoConfigEntity> LoadSsoAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseSsoConfigEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
        ?? throw new ValidationException("SSO Config 不存在", ErrorCodes.ParameterInvalid);

    private async Task<FlowiseRoleEntity> LoadRoleAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseRoleEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
        ?? throw new ValidationException("Role 不存在", ErrorCodes.ParameterInvalid);

    private async Task<FlowiseUserEntity> LoadUserAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseUserEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
        ?? throw new ValidationException("User 不存在", ErrorCodes.ParameterInvalid);

    private async Task<FlowiseLoginActivityEntity> LoadLoginActivityAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseLoginActivityEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
        ?? throw new ValidationException("Login Activity 不存在", ErrorCodes.ParameterInvalid);

    private async Task<FlowiseAuditLogEntity> LoadLogAsync(string id, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseAuditLogEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
        ?? throw new ValidationException("Log 不存在", ErrorCodes.ParameterInvalid);

    private async Task<long> CountChatFlowAsync(string type, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseChatFlowEntity>().CountAsync(item => !item.IsDeleted && item.Type == type, cancellationToken);

    private sealed record FlowiseShareTarget(string ItemId, string ItemType, string? WorkspaceId);

    private static string NormalizeSharedItemType(string value)
    {
        var normalized = value.Trim();
        return normalized.Equals(FlowiseSharedItemTypes.Tool, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Tool :
            normalized.Equals(FlowiseSharedItemTypes.Credential, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Credential :
            normalized.Equals(FlowiseSharedItemTypes.Variable, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Variable :
            normalized.Equals(FlowiseSharedItemTypes.ApiKey, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.ApiKey :
            normalized.Equals(FlowiseSharedItemTypes.Assistant, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Assistant :
            normalized.Equals(FlowiseSharedItemTypes.Marketplace, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Marketplace :
            normalized.Equals(FlowiseSharedItemTypes.DocumentStore, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.DocumentStore :
            normalized.Equals(FlowiseSharedItemTypes.Dataset, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Dataset :
            normalized.Equals(FlowiseSharedItemTypes.Evaluator, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Evaluator :
            normalized.Equals(FlowiseSharedItemTypes.Evaluation, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Evaluation :
            normalized.Equals(FlowiseSharedItemTypes.SsoConfig, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.SsoConfig :
            normalized.Equals(FlowiseSharedItemTypes.Role, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.Role :
            normalized.Equals(FlowiseSharedItemTypes.User, StringComparison.OrdinalIgnoreCase) ? FlowiseSharedItemTypes.User :
            normalized.ToUpperInvariant();
    }

    private static string NormalizeChatflowItemType(string value) => value.Trim().ToUpperInvariant();
}
