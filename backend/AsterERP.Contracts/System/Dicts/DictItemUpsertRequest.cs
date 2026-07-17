namespace AsterERP.Contracts.System.Dicts;

public sealed record DictItemUpsertRequest(
    string ItemLabel,
    string ItemValue,
    int SortOrder,
    bool IsEnabled,
    string? Remark);
