namespace AsterERP.Contracts.ApplicationConsole;

public sealed record ApplicationDatabaseBindingRequest(
    string Provider,
    string? ConnectionString = null,
    string? DisplayName = null,
    string? DatabaseName = null);
