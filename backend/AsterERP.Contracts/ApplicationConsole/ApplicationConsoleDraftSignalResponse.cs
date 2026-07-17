namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleDraftSignalResponse(
    string Code,
    string Title,
    string Detail,
    string Severity,
    int Count);
