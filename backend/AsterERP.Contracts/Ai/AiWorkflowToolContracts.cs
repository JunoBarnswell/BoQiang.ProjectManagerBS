namespace AsterERP.Contracts.Ai;

public sealed class AiWorkflowDraftArtifactDto
{
    public string Id { get; set; } = string.Empty;

    public string ConversationId { get; set; } = string.Empty;

    public string? RunId { get; set; }

    public string? PlanId { get; set; }

    public string? PlanItemId { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string WorkflowKey { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public string Status { get; set; } = "Draft";

    public string DraftDslJson { get; set; } = "{}";

    public string? BpmnXml { get; set; }

    public string? BusinessCanvasJson { get; set; }

    public string? BindingProposalJson { get; set; }

    public string? FormPermissionProposalJson { get; set; }

    public string? ActionMappingProposalJson { get; set; }

    public string? NotificationPreviewJson { get; set; }

    public string? ImportedWorkflowModelId { get; set; }

    public DateTime CreatedTime { get; set; }

    public DateTime? UpdatedTime { get; set; }
}

public sealed class AiWorkflowDraftNodeDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Type { get; set; } = "userTask";

    public IReadOnlyList<string> CandidateRoles { get; set; } = [];

    public IReadOnlyList<string> CandidateUsers { get; set; } = [];

    public string? Condition { get; set; }

    public int PositionX { get; set; }

    public int PositionY { get; set; }
}

public sealed class AiWorkflowDraftEdgeDto
{
    public string Id { get; set; } = string.Empty;

    public string SourceId { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Condition { get; set; }
}

public sealed class AiWorkflowDraftDto
{
    public string WorkflowKey { get; set; } = string.Empty;

    public string WorkflowName { get; set; } = string.Empty;

    public string BusinessType { get; set; } = string.Empty;

    public IReadOnlyList<AiWorkflowDraftNodeDto> Nodes { get; set; } = [];

    public IReadOnlyList<AiWorkflowDraftEdgeDto> Edges { get; set; } = [];

    public Dictionary<string, object?> Variables { get; set; } = [];
}

public sealed class AiWorkflowValidationIssueDto
{
    public string Severity { get; set; } = "Warning";

    public string ErrorCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? NodeId { get; set; }

    public string? EdgeId { get; set; }

    public string? Field { get; set; }

    public string? Suggestion { get; set; }
}

public sealed class AiWorkflowValidationReportDto
{
    public string Id { get; set; } = string.Empty;

    public string DraftArtifactId { get; set; } = string.Empty;

    public bool IsValid { get; set; }

    public int ErrorCount { get; set; }

    public int WarningCount { get; set; }

    public IReadOnlyList<AiWorkflowValidationIssueDto> Issues { get; set; } = [];

    public string TraceId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AiWorkflowSimulationStepDto
{
    public int SortOrder { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeName { get; set; } = string.Empty;

    public string Action { get; set; } = "enter";

    public string? MatchedEdgeId { get; set; }

    public string? Condition { get; set; }

    public bool ConditionMatched { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class AiWorkflowSimulationReportDto
{
    public string Id { get; set; } = string.Empty;

    public string DraftArtifactId { get; set; } = string.Empty;

    public bool Succeeded { get; set; }

    public Dictionary<string, object?> Variables { get; set; } = [];

    public IReadOnlyList<AiWorkflowSimulationStepDto> Steps { get; set; } = [];

    public string TraceId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AiWorkflowBindingProposalDto
{
    public string BusinessType { get; set; } = string.Empty;

    public string MenuCode { get; set; } = string.Empty;

    public string ProcessDefinitionKey { get; set; } = string.Empty;

    public string? FormResourceCode { get; set; }

    public string? PageCode { get; set; }

    public string? ModelCode { get; set; }

    public string? KeyField { get; set; }

    public string Summary { get; set; } = string.Empty;
}

public sealed class AiWorkflowNotificationPreviewDto
{
    public string NodeId { get; set; } = string.Empty;

    public string Trigger { get; set; } = string.Empty;

    public string ReceiverType { get; set; } = string.Empty;

    public string TemplateCode { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string Content { get; set; } = string.Empty;
}

public sealed class AiWorkflowDiagnosisReportDto
{
    public string Id { get; set; } = string.Empty;

    public string DiagnosisType { get; set; } = string.Empty;

    public string TargetId { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public IReadOnlyList<string> Evidence { get; set; } = [];

    public IReadOnlyList<string> Suggestions { get; set; } = [];

    public string TraceId { get; set; } = string.Empty;

    public DateTime CreatedTime { get; set; }
}

public sealed class AiWorkflowOverviewDto
{
    public IReadOnlyList<AiWorkflowDraftArtifactDto> DraftArtifacts { get; set; } = [];

    public IReadOnlyList<AiWorkflowValidationReportDto> ValidationReports { get; set; } = [];

    public IReadOnlyList<AiWorkflowSimulationReportDto> SimulationReports { get; set; } = [];

    public IReadOnlyList<AiWorkflowDiagnosisReportDto> DiagnosisReports { get; set; } = [];

    public IReadOnlyList<AiToolInvocationDto> ToolInvocations { get; set; } = [];
}
