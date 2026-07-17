namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationDatabaseBindingResolution(
    string Status,
    ApplicationDatabaseBindingOptions? Options,
    string? Message,
    bool IsLegacy);
