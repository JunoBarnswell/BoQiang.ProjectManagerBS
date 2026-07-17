namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleRecentItemResponse(
    string Id,
    string Title,
    string Description,
    DateTime CreatedTime,
    string Status);
