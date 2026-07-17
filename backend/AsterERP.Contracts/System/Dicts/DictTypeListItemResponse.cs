namespace AsterERP.Contracts.System.Dicts;

public sealed record DictTypeListItemResponse(
    string Id,
    string DictCode,
    string DictName,
    bool IsEnabled,
    string? Remark);
