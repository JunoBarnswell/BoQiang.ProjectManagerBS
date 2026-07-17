namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowMutationResponse
{
    public bool Succeeded { get; init; }

    public int AffectedRows { get; init; }

    public bool Conflict { get; init; }

    public IReadOnlyDictionary<string, object?> ServerValues { get; init; } = new Dictionary<string, object?>();

    public IReadOnlyDictionary<string, object?> LocalValues { get; init; } = new Dictionary<string, object?>();

    public string? ConflictMessage { get; init; }

    public bool CanRetry { get; init; }

    public bool CanOverwrite { get; init; }

    public string? AuditId { get; init; }

    public string? LedgerId { get; init; }

    public string? RequestHash { get; init; }

    public string? ExecutionStatus { get; init; }

    public bool RecoveryRequired { get; init; }
}
