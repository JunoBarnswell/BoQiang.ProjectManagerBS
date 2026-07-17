namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationConsoleMetricResponse(
    string Code,
    string Name,
    string Value,
    string Unit,
    string Status);
