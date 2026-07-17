namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationDatabaseBindingOptions(
    string Provider,
    string ConnectionString,
    string? DisplayName,
    string? DatabaseName,
    DateTime? UpdatedAt = null,
    string? UpdatedBy = null);
