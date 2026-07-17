namespace AsterERP.Workflow.Core.Security;

public record WorkflowGroup
{
    public string Id { get; init; } = null!;

    public string Name { get; init; } = null!;

    public string? Type { get; init; }

    public string? ParentId { get; init; }
}
