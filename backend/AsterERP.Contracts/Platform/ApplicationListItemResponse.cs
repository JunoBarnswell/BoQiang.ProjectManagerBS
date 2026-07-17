namespace AsterERP.Contracts.Platform;

public sealed record ApplicationListItemResponse(
    string Id,
    string AppCode,
    string AppName,
    string AppType,
    string? Icon,
    string? DefaultRoutePath,
    string? AdminDefaultRoutePath,
    string? RuntimeDefaultRoutePath,
    string Status,
    string? Version,
    string? Remark);
