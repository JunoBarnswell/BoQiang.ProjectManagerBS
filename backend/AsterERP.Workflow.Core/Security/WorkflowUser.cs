namespace AsterERP.Workflow.Core.Security;

public record WorkflowUser
{
    public string Id { get; init; } = null!;

    public string Username { get; init; } = null!;

    public string? Email { get; init; }

    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    public string? DisplayName { get; init; }

    public bool IsActive { get; init; } = true;

    public DateTime? LastLoginTime { get; init; }
}
