namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed class ApplicationDataSourceTableRowDeleteRequest
{
    public Dictionary<string, object?> KeyValues { get; set; } = [];

    public Dictionary<string, object?> OriginalValues { get; set; } = [];

    public object? VersionValue { get; set; }

    public string? ConflictResolution { get; set; }

    public string? AuditId { get; set; }

    public string? RequestHash { get; set; }

    public int? ExpectedAffectedRows { get; set; }

    public bool Confirmed { get; set; }
}
