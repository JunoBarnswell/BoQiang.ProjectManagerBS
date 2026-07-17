using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseMarketplaceService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseMarketplaceService
{
    public async Task<GridPageResult<FlowiseResourceDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseMarketplacesView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseMarketplaceTemplateEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.TemplateKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            dbQuery = dbQuery.Where(item => item.Category == category);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseResourceDto> { Total = total.Value, Items = rows.Select(Map).ToList() };
    }

    public async Task<FlowiseResourceDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseMarketplacesView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        return Map(await LoadAsync(id, cancellationToken));
    }

    public async Task<FlowiseResourceDto> CreateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseMarketplacesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        return await CreateCoreAsync(request, "marketplace.created", cancellationToken);
    }

    public async Task<FlowiseResourceDto> CreateFromFlowTemplateAsync(FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseTemplatesFlowExport, PermissionCodes.FlowiseMarketplacesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        return await CreateCoreAsync(request, "marketplace.flow-template.created", cancellationToken);
    }

    private async Task<FlowiseResourceDto> CreateCoreAsync(FlowiseResourceUpsertRequest request, string auditEvent, CancellationToken cancellationToken)
    {
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseMarketplaceTemplateEntity>().AnyAsync(item => !item.IsDeleted && item.TemplateKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Marketplace template key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseMarketplaceTemplateEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync(auditEvent, entity.Id, entity.TemplateKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseResourceDto> UpdateAsync(string id, FlowiseResourceUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseMarketplacesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseMarketplaceTemplateEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.TemplateKey == normalized.ResourceKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Marketplace template key 已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("marketplace.updated", entity.Id, entity.TemplateKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseMarketplacesEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("marketplace.deleted", entity.Id, entity.TemplateKey, cancellationToken);
        return true;
    }

    private static void Apply(FlowiseMarketplaceTemplateEntity entity, FlowiseResourceUpsertRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.TemplateKey = request.ResourceKey.Trim();
        entity.Name = request.DisplayName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.FlowData = FlowiseResourceJson.NormalizeObject(request.DefinitionJson, "Marketplace flowData");
        entity.MetadataJson = FlowiseResourceJson.NormalizeObject(request.MetadataJson, "Marketplace metadata");
    }

    private static FlowiseResourceUpsertRequest Normalize(FlowiseResourceUpsertRequest request)
    {
        request.ResourceKey = FlowiseResourceJson.Required(request.ResourceKey, "Marketplace template key");
        request.DisplayName = FlowiseResourceJson.Required(request.DisplayName, "Marketplace template name");
        return request;
    }

    private async Task<FlowiseMarketplaceTemplateEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseMarketplaceTemplateEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise Marketplace template 不存在", ErrorCodes.ParameterInvalid);
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
            ResourceType = "marketplace",
            ResourceId = resourceId,
            DetailJson = $$"""{"key":"{{detail}}"}"""
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static FlowiseResourceDto Map(FlowiseMarketplaceTemplateEntity entity) => new()
    {
        Id = entity.Id,
        ResourceType = "marketplace",
        ResourceKey = entity.TemplateKey,
        DisplayName = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        Category = entity.Category,
        Status = entity.Status,
        DefinitionJson = entity.FlowData,
        MetadataJson = entity.MetadataJson,
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}
