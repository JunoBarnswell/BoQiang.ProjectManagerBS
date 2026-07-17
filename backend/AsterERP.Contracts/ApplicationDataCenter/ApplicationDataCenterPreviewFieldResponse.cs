namespace AsterERP.Contracts.ApplicationDataCenter;

public sealed record ApplicationDataCenterPreviewFieldResponse(
    string FieldCode,
    string FieldName,
    string DataType,
    bool Nullable,
    bool PrimaryKey,
    int Order,
    string ValueKind = "scalar",
    IReadOnlyList<ApplicationDataCenterPreviewFieldResponse>? Children = null,
    string? DatasetKey = null,
    string? SourcePath = null);
