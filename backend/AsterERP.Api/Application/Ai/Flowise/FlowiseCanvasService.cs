using System.Text.Json;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseCanvasService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    FlowiseFlowDataValidator flowDataValidator,
    FlowisePermissionGuard permissionGuard) : IFlowiseCanvasService
{
    public Task<IReadOnlyList<FlowiseNodeCatalogItemDto>> GetNodeCatalogAsync(CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        return Task.FromResult<IReadOnlyList<FlowiseNodeCatalogItemDto>>(FlowiseCanvasNodeCatalog.Items);
    }

    public async Task<FlowiseCanvasDto> GetByResourceAsync(string resourceId, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(resourceId, cancellationToken);
        EnsureView(chatflow.Type);
        return new FlowiseCanvasDto
        {
            Id = chatflow.Id,
            ResourceId = chatflow.Id,
            FlowType = ToFlowType(chatflow.Type),
            FlowData = chatflow.FlowData,
            CreatedTime = chatflow.CreatedTime,
            UpdatedTime = chatflow.UpdatedTime
        };
    }

    public async Task<FlowiseCanvasDto> SaveAsync(FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureEdit(chatflow.Type);
        var validation = BuildValidationResult(request, chatflow.Type);
        if (!validation.Valid)
        {
            var firstError = validation.Issues.First(issue => issue.Severity == "error");
            throw new ValidationException(firstError.Message, ErrorCodes.ParameterInvalid);
        }

        var workspace = workspaceContext.Resolve();
        await db.Ado.BeginTranAsync();
        try
        {
            chatflow.FlowData = flowDataValidator.Normalize(request.FlowData);
            chatflow.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(chatflow).ExecuteCommandAsync(cancellationToken);

            await db.Insertable(new FlowiseAuditLogEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                WorkspaceId = chatflow.WorkspaceId,
                EventType = "canvas.saved",
                ResourceType = chatflow.Type,
                ResourceId = chatflow.Id,
                DetailJson = JsonSerializer.Serialize(new
                {
                    chatflow.Name,
                    protocol = "flowise.flowData",
                    validation.Issues.Count
                })
            }).ExecuteCommandAsync(cancellationToken);
            db.Ado.CommitTran();
            return new FlowiseCanvasDto
            {
                Id = chatflow.Id,
                ResourceId = chatflow.Id,
                FlowType = ToFlowType(chatflow.Type),
                FlowData = chatflow.FlowData,
                CreatedTime = chatflow.CreatedTime,
                UpdatedTime = chatflow.UpdatedTime,
                Validation = validation
            };
        }
        catch
        {
            db.Ado.RollbackTran();
            throw;
        }
    }

    public Task<FlowiseCanvasValidationResultDto> ValidateAsync(FlowiseCanvasUpsertRequest request, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAnyView();
        return Task.FromResult(BuildValidationResult(request));
    }

    private async Task<FlowiseChatFlowEntity> LoadChatFlowAsync(string resourceId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            throw new ValidationException("缺少 Flowise ChatFlow Id", ErrorCodes.ParameterInvalid);
        }

        return await db.Queryable<FlowiseChatFlowEntity>().FirstAsync(item => !item.IsDeleted && item.Id == resourceId.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);
    }

    private FlowiseCanvasValidationResultDto BuildValidationResult(FlowiseCanvasUpsertRequest request, string? resourceType = null)
    {
        var issues = new List<FlowiseCanvasValidationIssueDto>();
        if (string.IsNullOrWhiteSpace(request.ResourceId))
        {
            issues.Add(Error("missing_chatflow", "缺少 Flowise ChatFlow Id"));
        }

        var result = flowDataValidator.Validate(request.FlowData);
        foreach (var issue in result.Issues)
        {
            issues.Add(issue);
        }

        if (IsAgentflow(request.FlowType) || IsAgentflow(resourceType))
        {
            AddAgentflowStartNodeIssues(request.FlowData, issues);
        }

        return new FlowiseCanvasValidationResultDto
        {
            Valid = issues.All(issue => issue.Severity != "error"),
            Issues = issues
        };
    }

    private static string ToFlowType(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            return "Agentflow";
        }

        return "Chatflow";
    }

    private static bool IsAgentflow(string? flowType) =>
        string.Equals(flowType, "Agentflow", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(flowType, FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase);

    private static void AddAgentflowStartNodeIssues(string? flowData, ICollection<FlowiseCanvasValidationIssueDto> issues)
    {
        try
        {
            var json = string.IsNullOrWhiteSpace(flowData)
                ? """{"nodes":[],"edges":[]}"""
                : flowData.Trim();
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var startNodeIds = nodes
                .EnumerateArray()
                .Where(IsStartAgentflowNode)
                .Select(node => ReadString(node, "id"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();
            if (startNodeIds.Count == 0)
            {
                issues.Add(Error("missing_start_node", "工作流必须包含一个 Start 节点"));
                return;
            }

            if (startNodeIds.Count > 1)
            {
                issues.Add(Error("duplicate_start_node", "工作流只能包含一个 Start 节点", startNodeIds[1]));
            }
        }
        catch (JsonException)
        {
            // Base flowData validation already reports malformed JSON.
        }
    }

    private static bool IsStartAgentflowNode(JsonElement node)
    {
        if (!node.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var name = ReadString(data, "name");
        var nodeType = ReadString(data, "nodeType");
        var displayName = ReadString(data, "displayName");
        return string.Equals(name, "startAgentflow", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(nodeType, "startAgentflow", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(displayName, "Start", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static FlowiseCanvasValidationIssueDto Error(string code, string message, string? nodeId = null, string? edgeId = null) => new()
    {
        Code = code,
        EdgeId = edgeId,
        Message = message,
        NodeId = nodeId,
        Severity = "error"
    };

    private void EnsureView(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
    }

    private void EnsureEdit(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsEdit, PermissionCodes.FlowiseEdit, PermissionCodes.FlowiseManage);
    }
}
