namespace AsterERP.Contracts.System.Parameters;

/// <summary>
/// Request DTO used for both create and update operations on a system parameter.
/// </summary>
public sealed class ParameterUpsertRequest
{
    public string ParamName { get; set; } = string.Empty;

    public string ParamKey { get; set; } = string.Empty;

    public string ParamValue { get; set; } = string.Empty;

    public string Category { get; set; } = "general";

    public bool IsEnabled { get; set; } = true;

    public string? Remark { get; set; }
}
