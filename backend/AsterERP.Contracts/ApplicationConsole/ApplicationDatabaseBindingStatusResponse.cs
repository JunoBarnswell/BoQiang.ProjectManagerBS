using System.Text.Json.Serialization;

namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationDatabaseBindingStatusResponse(
    bool IsBound,
    bool IsReachable,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Provider,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DisplayName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DatabaseName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTime? UpdatedAt,
    bool CanManage,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Message,
    string Status = ApplicationDatabaseBindingStatus.NotConfigured);
