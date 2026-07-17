namespace AsterERP.Contracts.System.Dicts;

public sealed record DictTypeUpsertRequest(
    string DictCode,
    string DictName,
    bool IsEnabled,
    string? Remark);
