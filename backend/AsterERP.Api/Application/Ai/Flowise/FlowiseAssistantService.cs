using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise.Assistants;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseAssistantService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowisePermissionGuard permissionGuard) : IFlowiseAssistantService
{
    public async Task<GridPageResult<FlowiseAssistantDto>> GetPageAsync(FlowiseStudioQuery query, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAssistantsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        var dbQuery = db.Queryable<FlowiseAssistantEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.AssistantKey.Contains(keyword) || item.Name.Contains(keyword) || (item.Description != null && item.Description.Contains(keyword)));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            dbQuery = dbQuery.Where(item => item.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            dbQuery = dbQuery.Where(item => item.AssistantType == category);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.UpdatedTime ?? item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePage(query.PageIndex), NormalizeSize(query.PageSize), total);
        return new GridPageResult<FlowiseAssistantDto> { Total = total.Value, Items = rows.Select(Map).ToList() };
    }

    public async Task<FlowiseAssistantDto> GetAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAssistantsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
        return Map(await LoadAsync(id, cancellationToken));
    }

    public async Task<FlowiseAssistantDto> CreateAsync(FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAssistantsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseAssistantEntity>().AnyAsync(item => !item.IsDeleted && item.AssistantKey == normalized.AssistantKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Assistant key 已存在", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseAssistantEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId
        };
        Apply(entity, normalized);
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("assistant.created", entity.Id, entity.AssistantKey, cancellationToken);
        return Map(entity);
    }

    public async Task<FlowiseAssistantDto> UpdateAsync(string id, FlowiseAssistantUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAssistantsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        var normalized = Normalize(request);
        var duplicate = await db.Queryable<FlowiseAssistantEntity>().AnyAsync(item => !item.IsDeleted && item.Id != entity.Id && item.AssistantKey == normalized.AssistantKey, cancellationToken);
        if (duplicate)
        {
            throw new ValidationException("Flowise Assistant key 已存在", ErrorCodes.ParameterInvalid);
        }

        Apply(entity, normalized);
        entity.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("assistant.updated", entity.Id, entity.AssistantKey, cancellationToken);
        return Map(entity);
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseAssistantsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
        var entity = await LoadAsync(id, cancellationToken);
        entity.IsDeleted = true;
        entity.DeletedTime = DateTime.UtcNow;
        await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        await WriteAuditAsync("assistant.deleted", entity.Id, entity.AssistantKey, cancellationToken);
        return true;
    }

    private static void Apply(FlowiseAssistantEntity entity, FlowiseAssistantUpsertRequest request)
    {
        entity.WorkspaceId = string.IsNullOrWhiteSpace(request.WorkspaceId) ? null : request.WorkspaceId.Trim();
        entity.AssistantKey = request.AssistantKey.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.AssistantType = string.IsNullOrWhiteSpace(request.AssistantType) ? "custom" : request.AssistantType.Trim();
        entity.Status = FlowiseResourceJson.NormalizeStatus(request.Status);
        entity.DefinitionJson = SerializeDefinition(NormalizeDefinition(request.Definition));
        entity.MetadataJson = FlowiseResourceJson.NormalizeObject(request.AdvancedMetadataJson, "Assistant advanced metadata");
    }

    private static FlowiseAssistantUpsertRequest Normalize(FlowiseAssistantUpsertRequest request)
    {
        request.AssistantKey = FlowiseResourceJson.Required(request.AssistantKey, "Assistant key");
        request.Name = FlowiseResourceJson.Required(request.Name, "Assistant name");
        request.Definition = NormalizeDefinition(request.Definition);
        return request;
    }

    private async Task<FlowiseAssistantEntity> LoadAsync(string id, CancellationToken cancellationToken)
    {
        return await db.Queryable<FlowiseAssistantEntity>().FirstAsync(item => !item.IsDeleted && item.Id == id.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise Assistant 不存在", ErrorCodes.ParameterInvalid);
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
            ResourceType = "assistant",
            ResourceId = resourceId,
            DetailJson = $$"""{"key":"{{detail}}"}"""
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static FlowiseAssistantDto Map(FlowiseAssistantEntity entity) => new()
    {
        Id = entity.Id,
        AssistantKey = entity.AssistantKey,
        Name = entity.Name,
        Description = entity.Description,
        WorkspaceId = entity.WorkspaceId,
        AssistantType = entity.AssistantType,
        Status = entity.Status,
        Definition = DeserializeDefinition(entity.DefinitionJson),
        AdvancedMetadataJson = FlowiseResourceJson.NormalizeObject(entity.MetadataJson, "Assistant advanced metadata"),
        CreatedTime = entity.CreatedTime,
        UpdatedTime = entity.UpdatedTime
    };

    private static FlowiseAssistantDefinitionDto NormalizeDefinition(FlowiseAssistantDefinitionDto? definition)
    {
        definition ??= new FlowiseAssistantDefinitionDto();
        definition.FileIds = NormalizeStringList(definition.FileIds);
        definition.Tools = NormalizeStringList(definition.Tools);
        definition.Instructions = string.IsNullOrWhiteSpace(definition.Instructions) ? null : definition.Instructions.Trim();
        definition.Model = string.IsNullOrWhiteSpace(definition.Model) ? null : definition.Model.Trim();
        definition.ResponseFormat = string.IsNullOrWhiteSpace(definition.ResponseFormat) ? null : definition.ResponseFormat.Trim();
        return definition;
    }

    private static IReadOnlyList<string> NormalizeStringList(IReadOnlyList<string>? values)
    {
        return values?
            .Select(value => value.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    private static string SerializeDefinition(FlowiseAssistantDefinitionDto definition)
    {
        return JsonSerializer.Serialize(definition);
    }

    private static FlowiseAssistantDefinitionDto DeserializeDefinition(string? definitionJson)
    {
        try
        {
            var definition = JsonSerializer.Deserialize<FlowiseAssistantDefinitionDto>(
                string.IsNullOrWhiteSpace(definitionJson) ? "{}" : definitionJson,
                new JsonSerializerOptions(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true });
            return NormalizeDefinition(definition);
        }
        catch
        {
            return new FlowiseAssistantDefinitionDto();
        }
    }

    private static int NormalizePage(int value) => value <= 0 ? 1 : value;

    private static int NormalizeSize(int value) => Math.Clamp(value <= 0 ? 20 : value, 1, 500);
}
