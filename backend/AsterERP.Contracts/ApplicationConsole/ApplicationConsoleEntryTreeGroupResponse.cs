namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleEntryTreeGroupResponse(
    string Key,
    string Title,
    string Description,
    string Icon,
    IReadOnlyList<ApplicationConsoleEntryTreeItemResponse> Items);
