using System.Text.Json;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public sealed class WorkflowBusinessCanvasDraftMapper
{
    public string Map(AiWorkflowDraftDto draft)
    {
        var businessDesign = MapBusinessDesign(draft);
        var canvas = new
        {
            workflowKey = draft.WorkflowKey,
            workflowName = draft.WorkflowName,
            businessDesign
        };
        return JsonSerializer.Serialize(canvas, WorkflowJsonOptions.Options);
    }

    public (JsonElement BusinessDesign, string CanvasJson) ResolveDesignerCanvas(
        string? businessCanvasJson,
        AiWorkflowDraftDto draft)
    {
        var canvasJson = string.IsNullOrWhiteSpace(businessCanvasJson)
            ? Map(draft)
            : businessCanvasJson;
        if (TryReadDesignerCanvas(canvasJson, out var businessDesign))
        {
            return (businessDesign, canvasJson);
        }

        canvasJson = Map(draft);
        if (TryReadDesignerCanvas(canvasJson, out businessDesign))
        {
            return (businessDesign, canvasJson);
        }

        throw new JsonException("AI Workflow 业务画布无法转换为设计器结构");
    }

    private static WorkflowBusinessDesignCanvas MapBusinessDesign(AiWorkflowDraftDto draft)
    {
        var nodes = draft.Nodes.Select(MapNode).ToList();
        var selectedNodeId = nodes.FirstOrDefault(item => item.Type == "approval")?.Id
                             ?? nodes.FirstOrDefault()?.Id
                             ?? string.Empty;
        return new WorkflowBusinessDesignCanvas
        {
            Version = "latest",
            SelectedNodeId = selectedNodeId,
            FormContext = null,
            Nodes = nodes,
            Edges = draft.Edges.Select(MapEdge).ToList()
        };
    }

    private static WorkflowBusinessNodeCanvas MapNode(AiWorkflowDraftNodeDto node)
    {
        var nodeType = MapNodeType(node.Type);
        var role = node.CandidateRoles.FirstOrDefault();
        var user = node.CandidateUsers.FirstOrDefault();
        var participantType = !string.IsNullOrWhiteSpace(role)
            ? "role"
            : !string.IsNullOrWhiteSpace(user) ? "user" : "dynamic";
        var participantId = role ?? user ?? string.Empty;
        return new WorkflowBusinessNodeCanvas
        {
            Id = node.Id,
            Type = nodeType,
            Label = node.Name,
            Position = new WorkflowBusinessPosition(node.PositionX, node.PositionY),
            ParticipantType = nodeType == "approval" ? participantType : "dynamic",
            ParticipantId = nodeType == "approval" ? participantId : string.Empty,
            ParticipantName = nodeType == "approval" ? participantId : string.Empty,
            ParticipantCode = nodeType == "approval" ? participantId : string.Empty,
            GroupKey = nodeType == "approval" && !string.IsNullOrWhiteSpace(role) ? role : null,
            ApprovalMode = node.Name.Contains("会签", StringComparison.OrdinalIgnoreCase) ? "all" : "all",
            ConditionExpression = node.Condition ?? string.Empty,
            Actions = nodeType == "approval"
                ? ["complete", "reject", "return", "transfer", "delegate"]
                : []
        };
    }

    private static WorkflowBusinessEdgeCanvas MapEdge(AiWorkflowDraftEdgeDto edge) =>
        new()
        {
            Id = edge.Id,
            Source = edge.SourceId,
            Target = edge.TargetId,
            Label = edge.Name,
            ConditionExpression = edge.Condition
        };

    private static string MapNodeType(string type)
    {
        if (type.Equals("startEvent", StringComparison.OrdinalIgnoreCase))
        {
            return "start";
        }

        if (type.Equals("endEvent", StringComparison.OrdinalIgnoreCase))
        {
            return "end";
        }

        if (type.Equals("exclusiveGateway", StringComparison.OrdinalIgnoreCase) ||
            type.Equals("condition", StringComparison.OrdinalIgnoreCase))
        {
            return "condition";
        }

        return "approval";
    }

    private static bool TryReadDesignerCanvas(
        string canvasJson,
        out JsonElement businessDesign)
    {
        businessDesign = default;
        try
        {
            using var document = JsonDocument.Parse(canvasJson);
            if (!document.RootElement.TryGetProperty("businessDesign", out var design) ||
                !design.TryGetProperty("nodes", out var nodes) ||
                nodes.ValueKind != JsonValueKind.Array ||
                nodes.GetArrayLength() == 0)
            {
                return false;
            }

            businessDesign = design.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }


    private sealed class WorkflowBusinessDesignCanvas
    {
        public string Version { get; set; } = "latest";

        public string SelectedNodeId { get; set; } = string.Empty;

        public object? FormContext { get; set; }

        public IReadOnlyList<WorkflowBusinessNodeCanvas> Nodes { get; set; } = [];

        public IReadOnlyList<WorkflowBusinessEdgeCanvas> Edges { get; set; } = [];
    }

    private sealed class WorkflowBusinessNodeCanvas
    {
        public string Id { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public WorkflowBusinessPosition Position { get; set; } = new(0, 0);

        public string ParticipantType { get; set; } = "dynamic";

        public string ParticipantId { get; set; } = string.Empty;

        public string ParticipantName { get; set; } = string.Empty;

        public string ParticipantCode { get; set; } = string.Empty;

        public string? GroupKey { get; set; }

        public string ApprovalMode { get; set; } = "all";

        public string ConditionExpression { get; set; } = string.Empty;

        public IReadOnlyList<string> Actions { get; set; } = [];
    }

    private sealed class WorkflowBusinessEdgeCanvas
    {
        public string Id { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string? Label { get; set; }

        public string? ConditionExpression { get; set; }
    }

    private sealed record WorkflowBusinessPosition(int X, int Y);
}
