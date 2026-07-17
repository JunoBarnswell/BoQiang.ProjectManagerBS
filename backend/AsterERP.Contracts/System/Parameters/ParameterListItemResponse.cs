namespace AsterERP.Contracts.System.Parameters;

/// <summary>
/// Represents a single system parameter returned by list and write operations.
/// </summary>
public sealed record ParameterListItemResponse(
    string Id,
    string ParamName,
    string ParamKey,
    string ParamValue,
    bool IsSensitive,
    string Category,
    bool IsEnabled,
    string? Remark);
