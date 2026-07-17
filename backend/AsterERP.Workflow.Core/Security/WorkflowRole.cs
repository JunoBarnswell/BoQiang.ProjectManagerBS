namespace AsterERP.Workflow.Core.Security;

public record WorkflowRole
{
    public string Id { get; init; } = null!;

    public string Name { get; init; } = null!;

    public List<string> Permissions { get; init; } = new();
}
