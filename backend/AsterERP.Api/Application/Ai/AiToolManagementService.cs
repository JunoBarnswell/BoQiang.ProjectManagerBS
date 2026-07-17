using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.Workflows;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiToolManagementService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    AiKernelFunctionCatalog catalog,
    IWorkflowModelAppService workflowModelService)
{
    public async Task<GridPageResult<AiToolDefinitionDto>> GetDefinitionsAsync(AiToolDefinitionQuery query, CancellationToken cancellationToken = default)
    {
        await SyncCatalogDefinitionsAsync(cancellationToken);
        var dbQuery = db.Queryable<AiToolDefinitionEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            dbQuery = dbQuery.Where(item => item.ToolCode.Contains(keyword) || item.ToolName.Contains(keyword) || item.ToolDomain.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.ToolType))
        {
            var toolType = query.ToolType.Trim();
            dbQuery = dbQuery.Where(item => item.ToolType == toolType);
        }

        if (!string.IsNullOrWhiteSpace(query.ToolDomain))
        {
            var toolDomain = query.ToolDomain.Trim();
            dbQuery = dbQuery.Where(item => item.ToolDomain == toolDomain);
        }

        if (!string.IsNullOrWhiteSpace(query.RiskLevel))
        {
            var risk = query.RiskLevel.Trim();
            dbQuery = dbQuery.Where(item => item.RiskLevel == risk);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.ToolDomain)
            .OrderBy(item => item.ToolCode)
            .ToPageListAsync(Math.Max(query.PageIndex, 1), Math.Clamp(query.PageSize, 1, 200), total);
        return new GridPageResult<AiToolDefinitionDto> { Total = total.Value, Items = rows.Select(MapToolDefinition).ToList() };
    }

    public async Task<AiToolDefinitionDto> UpsertDefinitionAsync(string? id, AiToolDefinitionUpsertRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var entity = string.IsNullOrWhiteSpace(id)
            ? null
            : await db.Queryable<AiToolDefinitionEntity>().FirstAsync(item => item.Id == id && !item.IsDeleted, cancellationToken);
        entity = await UpsertToolDefinitionAsync(db, workspace, request, cancellationToken, entity);
        await WriteAuditAsync("tool.definition.saved", "AiToolDefinition", entity.Id, entity.ToolCode, cancellationToken);
        return MapToolDefinition(entity);
    }

    public async Task<IReadOnlyList<AiToolBindingDto>> GetBindingsAsync(string? agentProfileId, CancellationToken cancellationToken = default)
    {
        var query = db.Queryable<AiToolBindingEntity>().Where(item => !item.IsDeleted);
        if (!string.IsNullOrWhiteSpace(agentProfileId))
        {
            var id = agentProfileId.Trim();
            query = query.Where(item => item.AgentProfileId == id);
        }

        var rows = await query.OrderBy(item => item.AgentProfileId).OrderBy(item => item.ToolCode).ToListAsync(cancellationToken);
        return rows.Select(MapBinding).ToList();
    }

    public async Task<AiToolBindingDto> UpsertBindingAsync(AiToolBindingUpsertRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.AgentProfileId) || string.IsNullOrWhiteSpace(request.ToolCode))
        {
            throw new ValidationException("工具绑定缺少智能体或工具编码", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var toolCode = request.ToolCode.Trim();
        var agentProfileId = request.AgentProfileId.Trim();
        var existing = await db.Queryable<AiToolBindingEntity>()
            .FirstAsync(item => !item.IsDeleted && item.AgentProfileId == agentProfileId && item.ToolCode == toolCode, cancellationToken);
        var entity = existing ?? new AiToolBindingEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            AgentProfileId = agentProfileId,
            ToolCode = toolCode
        };
        entity.AutoInvokeAllowed = request.AutoInvokeAllowed;
        entity.Status = NormalizeStatus(request.Status);
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        await WriteAuditAsync("tool.binding.saved", "AiToolBinding", entity.Id, $"{agentProfileId}:{toolCode}", cancellationToken);
        return MapBinding(entity);
    }

    public async Task<IReadOnlyList<AiWorkflowOptionDto>> GetAvailableWorkflowsAsync(CancellationToken cancellationToken = default)
    {
        var page = await workflowModelService.GetPageAsync(new GridQuery { PageIndex = 1, PageSize = 200 }, cancellationToken);
        return page.Items.Select(item => new AiWorkflowOptionDto
        {
            WorkflowModelId = item.ModelId,
            WorkflowCode = item.ModelKey,
            WorkflowName = item.Name,
            Status = item.Status?.ToString() ?? "Unknown"
        }).ToList();
    }

    public async Task<AiWorkflowToolBindingDto> BindWorkflowAsync(AiWorkflowToolBindingRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.WorkflowModelId) || string.IsNullOrWhiteSpace(request.WorkflowCode) || string.IsNullOrWhiteSpace(request.ToolCode))
        {
            throw new ValidationException("Workflow 工具绑定缺少流程或工具编码", ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        var workflowModelId = request.WorkflowModelId.Trim();
        var toolCode = request.ToolCode.Trim();
        var existing = await db.Queryable<AiWorkflowToolBindingEntity>()
            .FirstAsync(item => !item.IsDeleted && item.WorkflowModelId == workflowModelId && item.ToolCode == toolCode, cancellationToken);
        var entity = existing ?? new AiWorkflowToolBindingEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            WorkflowModelId = workflowModelId,
            ToolCode = toolCode
        };
        entity.WorkflowCode = request.WorkflowCode.Trim();
        entity.WorkflowName = string.IsNullOrWhiteSpace(request.WorkflowName) ? request.WorkflowCode.Trim() : request.WorkflowName.Trim();
        entity.RiskLevel = NormalizeRisk(request.RiskLevel);
        entity.RequiresConfirmation = entity.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase) || request.RequiresConfirmation;
        entity.Status = NormalizeStatus(request.Status);
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        await WriteAuditAsync("workflow-tool.binding.saved", "AiWorkflowToolBinding", entity.Id, $"{entity.WorkflowCode}:{toolCode}", cancellationToken);
        return MapWorkflowBinding(entity);
    }

    public async Task SyncCatalogDefinitionsAsync(CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        foreach (var definition in catalog.ListDefinitions())
        {
            var request = new AiToolDefinitionUpsertRequest
            {
                ToolCode = definition.ToolCode,
                ToolName = definition.ToolName,
                ToolType = ResolveToolType(definition.ToolDomain, definition.ToolCode),
                ToolDomain = definition.ToolDomain,
                RiskLevel = NormalizeRisk(definition.RiskLevel),
                RequiresConfirmation = definition.RequiresConfirmation,
                PermissionCode = definition.PermissionCode,
                InputSchemaJson = definition.InputSchemaJson,
                OutputSchemaJson = definition.OutputSchemaJson,
                Status = definition.IsEnabled ? "Enabled" : "Disabled"
            };
            await UpsertToolDefinitionAsync(db, workspace, request, cancellationToken);
        }
    }

    public static async Task<AiToolDefinitionEntity> UpsertToolDefinitionAsync(
        ISqlSugarClient db,
        AiWorkspace workspace,
        AiToolDefinitionUpsertRequest request,
        CancellationToken cancellationToken,
        AiToolDefinitionEntity? existingEntity = null)
    {
        if (string.IsNullOrWhiteSpace(request.ToolCode) || string.IsNullOrWhiteSpace(request.ToolName))
        {
            throw new ValidationException("工具定义缺少编码或名称", ErrorCodes.ParameterInvalid);
        }

        var toolCode = request.ToolCode.Trim();
        var existing = existingEntity ?? await db.Queryable<AiToolDefinitionEntity>()
            .FirstAsync(item => !item.IsDeleted && item.ToolCode == toolCode, cancellationToken);
        var entity = existing ?? new AiToolDefinitionEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            ToolCode = toolCode
        };
        entity.ToolName = request.ToolName.Trim();
        entity.ToolType = string.IsNullOrWhiteSpace(request.ToolType) ? "Api" : request.ToolType.Trim();
        entity.ToolDomain = string.IsNullOrWhiteSpace(request.ToolDomain) ? "general" : request.ToolDomain.Trim();
        entity.RiskLevel = NormalizeRisk(request.RiskLevel);
        entity.RequiresConfirmation = entity.RiskLevel.Equals("high", StringComparison.OrdinalIgnoreCase) || request.RequiresConfirmation;
        entity.PermissionCode = request.PermissionCode.Trim();
        entity.InputSchemaJson = string.IsNullOrWhiteSpace(request.InputSchemaJson) ? "{}" : request.InputSchemaJson.Trim();
        entity.OutputSchemaJson = string.IsNullOrWhiteSpace(request.OutputSchemaJson) ? "{}" : request.OutputSchemaJson.Trim();
        entity.Status = NormalizeStatus(request.Status);
        entity.UpdatedTime = existing is null ? null : DateTime.UtcNow;

        if (existing is null)
        {
            await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        }
        else
        {
            await db.Updateable(entity).ExecuteCommandAsync(cancellationToken);
        }

        return entity;
    }

    public static AiToolDefinitionDto MapToolDefinition(AiToolDefinitionEntity entity) => new()
    {
        Id = entity.Id,
        ToolCode = entity.ToolCode,
        ToolName = entity.ToolName,
        ToolType = entity.ToolType,
        ToolDomain = entity.ToolDomain,
        RiskLevel = entity.RiskLevel,
        RequiresConfirmation = entity.RequiresConfirmation,
        PermissionCode = entity.PermissionCode,
        InputSchemaJson = entity.InputSchemaJson,
        OutputSchemaJson = entity.OutputSchemaJson,
        Status = entity.Status,
        CreatedTime = entity.CreatedTime
    };

    private async Task WriteAuditAsync(string eventType, string resourceType, string? resourceId, string detail, CancellationToken cancellationToken)
    {
        var workspace = workspaceContext.Resolve();
        await db.Insertable(new AiAuditEventEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            EventType = eventType,
            ResourceType = resourceType,
            ResourceId = resourceId,
            UserId = workspace.UserId,
            DetailJson = global::System.Text.Json.JsonSerializer.Serialize(new { detail })
        }).ExecuteCommandAsync(cancellationToken);
    }

    private static AiToolBindingDto MapBinding(AiToolBindingEntity entity) => new()
    {
        Id = entity.Id,
        AgentProfileId = entity.AgentProfileId,
        ToolCode = entity.ToolCode,
        AutoInvokeAllowed = entity.AutoInvokeAllowed,
        Status = entity.Status
    };

    private static AiWorkflowToolBindingDto MapWorkflowBinding(AiWorkflowToolBindingEntity entity) => new()
    {
        Id = entity.Id,
        WorkflowModelId = entity.WorkflowModelId,
        WorkflowCode = entity.WorkflowCode,
        WorkflowName = entity.WorkflowName,
        ToolCode = entity.ToolCode,
        RiskLevel = entity.RiskLevel,
        RequiresConfirmation = entity.RequiresConfirmation,
        Status = entity.Status
    };

    private static string ResolveToolType(string domain, string toolCode)
    {
        if (domain.Equals("workflow", StringComparison.OrdinalIgnoreCase) || toolCode.StartsWith("workflow.", StringComparison.OrdinalIgnoreCase))
        {
            return "Workflow";
        }

        if (toolCode.Contains("search", StringComparison.OrdinalIgnoreCase) || toolCode.Contains("get", StringComparison.OrdinalIgnoreCase))
        {
            return "Query";
        }

        return "Api";
    }

    private static string NormalizeRisk(string? riskLevel)
    {
        var value = string.IsNullOrWhiteSpace(riskLevel) ? "low" : riskLevel.Trim();
        return value.ToUpperInvariant() switch
        {
            "L0" or "L1" or "LOW" => "low",
            "L2" or "MEDIUM" => "medium",
            "L3" or "L4" or "HIGH" => "high",
            _ => value.ToLowerInvariant()
        };
    }

    private static string NormalizeStatus(string? status) =>
        string.IsNullOrWhiteSpace(status) ? "Enabled" : status.Trim();
}
