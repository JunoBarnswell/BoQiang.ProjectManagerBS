namespace AsterERP.Contracts.System.Dicts;

public sealed record DictItemListItemResponse(
    string Id,
    string DictTypeId,
    string ItemLabel,
    string ItemValue,
    int SortOrder,
    bool IsEnabled,
    string? Remark);
