using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseVariableService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiSecretProtector secretProtector,
    FlowisePermissionGuard permissionGuard) : IFlowiseVariableService
{
    public async Task<GridPageResult<FlowiseResourceDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseVariablesView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseVariableEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.VariableKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
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
        permissionGuard.EnsureAny(PermissionCodes.FlowiseVariablesView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        return Map(await LoadAsync(id, cancellationToken));
    }

    public async Task<FlowiseResourceDto> CreateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseVariablesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseVariableEntity>().AnyAsync(item => !item.IsDeleted && item.VariableKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Variable key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseVariableEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("variable.created", entity.Id, entity.VariableKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseVariablesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseVariableEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.VariableKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Variable key 已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("variable.updated", entity.Id, entity.VariableKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseVariablesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("variable.deleted", entity.Id, entity.VariableKey, cancellationToken);
        return true;
    }

    public async Task<FlowiseResourceDto> RevealAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureSecretReveal();
        var entity = await LoadAsync(id, cancellationToken);
        var secret = string.IsNullOrWhiteSpace(entity.SecretCipherText) ? string.Empty : secretProtector.Unprotect(entity.SecretCipherText);
        await WriteAuditAsync("variable.revealed", entity.Id, entity.VariableKey, cancellationToken);
        return Map(entity, secret);
    }

    private void Apply(FlowiseVariableEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.VariableKey = request.ResourceKey.Trim();
        entity.Name = request.DisplayName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Scope = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.ValueJson = FlowiseResourceJson.NormalizeObject(request.DefinitionJson, "Variable value");
        entity.MetadataJson = FlowiseResourceJson.NormalizeObject(request.MetadataJson, "Variable metadata");
        if (!string.IsNullOrWhiteSpace(request.SecretValue))
        {
            entity.IsSecret = true;
            entity.SecretCipherText = secretProtector.Protect(request.SecretValue);
            entity.SecretHash = FlowiseResourceJson.Sha256(request.SecretValue);
            entity.SecretMask = secretProtector.Mask(request.SecretValue);
        }
    }

    private static FlowiseResourceUpsertRequest Normalize(FlowiseResourceUpsertRequest request)
    {
        request.ResourceKey = FlowiseResourceJson.Required(request.ResourceKey, "Variable key");
        request.DisplayName = FlowiseResourceJson.Required(request.DisplayName, "Variable name");
        return request;
    }

    private async Task<FlowiseVariableEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseVariableEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise Variable 不存在", ErrorCodes.ParameterInvalid);
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
            ResourceType = "variable",
            ResourceId = resourceId,
            DetailJson = $$"""{"key":"{{detail}}"}"""
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static FlowiseResourceDto Map(FlowiseVariableEntity entity, string? oneTimeSecret = null) => new()
    {
        Id = entity.Id,
        ResourceType = "variable",
        ResourceKey = entity.VariableKey,
        DisplayName = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Category = entity.Scope,
        Status = entity.Status,
        DefinitionJson = entity.IsSecret ? "{}" : entity.ValueJson,
        MetadataJson = entity.MetadataJson,
        SecretMask = entity.SecretMask,
        OneTimeSecret = oneTimeSecret,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}
