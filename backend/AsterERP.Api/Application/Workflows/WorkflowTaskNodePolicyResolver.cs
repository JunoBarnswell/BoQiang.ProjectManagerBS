using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using AsterERP.Api.Infrastructure.Database;
using AsterERP.Contracts.Workflows;
using AsterERP.Workflow.Persistence.Entities;

namespace AsterERP.Api.Application.Workflows;

public sealed class WorkflowTaskNodePolicyResolver(IWorkspaceDatabaseAccessor databaseAccessor)
{
    public async Task<WorkflowTaskNodePolicyResponse> ResolveAsync(
        string? processDefinitionId,
        string? taskDefinitionKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(processDefinitionId) || string.IsNullOrWhiteSpace(taskDefinitionKey))
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }

        var db = databaseAccessor.GetCurrentDb();
        var definition = await db.Queryable<ProcessDefinitionEntity>()
            .FirstAsync(item => item.Id == processDefinitionId, cancellationToken);
        if (definition is null ||
            string.IsNullOrWhiteSpace(definition.DeploymentId) ||
            string.IsNullOrWhiteSpace(definition.ResourceName))
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }

        var resource = await db.Queryable<ResourceEntity>()
            .FirstAsync(item => item.DeploymentId == definition.DeploymentId && item.Name == definition.ResourceName, cancellationToken);
        if (resource?.Bytes is not { Length: > 0 })
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }

        return ResolveFromBpmnXml(Encoding.UTF8.GetString(resource.Bytes), taskDefinitionKey);
    }

    private static WorkflowTaskNodePolicyResponse ResolveFromBpmnXml(string bpmnXml, string taskDefinitionKey)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(bpmnXml);
        }
        catch
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }

        var taskNode = document.Descendants()
            .FirstOrDefault(item => string.Equals((string?)item.Attribute("id"), taskDefinitionKey, StringComparison.OrdinalIgnoreCase));
        var nodeConfig = taskNode?.Descendants().FirstOrDefault(item => item.Name.LocalName == "nodeConfig")?.Value;
        if (string.IsNullOrWhiteSpace(nodeConfig))
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }

        try
        {
            using var json = JsonDocument.Parse(nodeConfig);
            var root = json.RootElement;
            return new WorkflowTaskNodePolicyResponse(
                taskDefinitionKey,
                ReadActionPolicies(root),
                ReadFieldPermissions(root));
        }
        catch (JsonException)
        {
            return WorkflowTaskNodePolicyResponse.Empty(taskDefinitionKey);
        }
    }

    private static IReadOnlyList<WorkflowTaskActionPolicyResponse> ReadActionPolicies(JsonElement root)
    {
        if (!root.TryGetProperty("actionPolicies", out var policies) || policies.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return policies.EnumerateArray()
            .Select(item => new WorkflowTaskActionPolicyResponse(
                ReadString(item, "action") ?? string.Empty,
                ReadBoolean(item, "enabled", true),
                ReadBoolean(item, "commentRequired", false),
                ReadString(item, "attachmentPolicy") ?? "optional"))
            .Where(item => !string.IsNullOrWhiteSpace(item.Action))
            .ToList();
    }

    private static IReadOnlyList<WorkflowTaskFieldPermissionResponse> ReadFieldPermissions(JsonElement root)
    {
        if (!root.TryGetProperty("fieldPermissions", out var permissions) || permissions.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return permissions.EnumerateArray()
            .Select(item => new WorkflowTaskFieldPermissionResponse(
                ReadString(item, "fieldKey") ?? ReadString(item, "field") ?? string.Empty,
                ReadString(item, "fieldLabel") ?? ReadString(item, "label"),
                ReadBoolean(item, "visible", true),
                ReadBoolean(item, "readonly", false),
                ReadBoolean(item, "required", false),
                ReadBoolean(item, "hidden", false)))
            .Where(item => !string.IsNullOrWhiteSpace(item.FieldKey))
            .ToList();
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static bool ReadBoolean(JsonElement element, string propertyName, bool fallback)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : fallback;
    }
}
