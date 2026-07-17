namespace AsterERP.Contracts.Workflows;

public sealed record WorkflowParticipantResponse(
    string Id,
    string Code,
    string Name,
    string Type,
    string? ParentId,
    string? GroupKey,
    string? Description,
    string? EmploymentSummary = null);
