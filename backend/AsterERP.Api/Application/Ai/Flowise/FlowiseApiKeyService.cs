using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseApiKeyService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseApiKeyService
{
    public async Task<GridPageResult<FlowiseResourceDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseApiKeysView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseApiKeyEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ApiKeyCode.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseResourceDto> { Total = total.Value, Items = rows.Select(item => Map(item)).ToList() };
    }

    public async Task<FlowiseResourceDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseApiKeysView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        return Map(await LoadAsync(id, cancellationToken));
    }

    public async Task<FlowiseResourceDto> CreateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseApiKeysEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseApiKeyEntity>().AnyAsync(item => !item.IsDeleted && item.ApiKeyCode == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise API Key code 已存在", ErrorCodes.ParameterInvalid);
        }

        var plainKey = string.IsNullOrWhiteSpace(normalized.SecretValue) ? FlowiseResourceJson.NewApiKey() : normalized.SecretValue.Trim();
        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseApiKeyEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized, plainKey);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("api-key.created", entity.Id, entity.ApiKeyCode, cancellationToken);
        return Map(entity, plainKey);
    }

    public async Task<FlowiseResourceDto> UpdateAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseApiKeysEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseApiKeyEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.ApiKeyCode == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise API Key code 已存在", ErrorCodes.ParameterInvalid);
        }

        var replacementKey = string.IsNullOrWhiteSpace(normalized.SecretValue) ? null : normalized.SecretValue.Trim();
        Apply(entity, normalized, replacementKey);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("api-key.updated", entity.Id, entity.ApiKeyCode, cancellationToken);
        return Map(entity, replacementKey);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseApiKeysEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("api-key.deleted", entity.Id, entity.ApiKeyCode, cancellationToken);
        return true;
    }

    private static void Apply(FlowiseApiKeyEntity entity, FlowiseResourceUpsertRequest request, string? plainKey)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.ApiKeyCode = request.ResourceKey.Trim();
        entity.Name = request.DisplayName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.MetadataJson = FlowiseResourceJson.NormalizeObject(request.MetadataJson, "API Key metadata");
        if (!string.IsNullOrWhiteSpace(plainKey))
        {
            entity.KeyHash = FlowiseResourceJson.Sha256(plainKey);
            entity.KeyMask = Mask(plainKey);
        }
    }

    private static FlowiseResourceUpsertRequest Normalize(FlowiseResourceUpsertRequest request)
    {
        request.ResourceKey = FlowiseResourceJson.Required(request.ResourceKey, "API Key code");
        request.DisplayName = FlowiseResourceJson.Required(request.DisplayName, "API Key name");
        return request;
    }

    private async Task<FlowiseApiKeyEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseApiKeyEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise API Key 不存在", ErrorCodes.ParameterInvalid);
    }

    private async Task WriteAuditAsync(string eventType, string resourceId, string detail, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new FlowiseAuditLogEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            EventType = eventType,
            ResourceType = "api-key",
            ResourceId = resourceId,
            DetailJson = $$"""{"key":"{{detail}}"}"""
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static FlowiseResourceDto Map(FlowiseApiKeyEntity entity, string? oneTimeSecret = null) => new()
    {
        Id = entity.Id,
        ResourceType = "api-key",
        ResourceKey = entity.ApiKeyCode,
        DisplayName = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Status = entity.Status,
        DefinitionJson = "{}",
        MetadataJson = entity.MetadataJson,
        SecretMask = entity.KeyMask,
        OneTimeSecret = oneTimeSecret,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static string Mask(string value)
    {
        var normalized = value.Trim();
        return normalized.Length <= 10 ? "****" : $"{normalized[..6]}****{normalized[^4..]}";
    }

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}
