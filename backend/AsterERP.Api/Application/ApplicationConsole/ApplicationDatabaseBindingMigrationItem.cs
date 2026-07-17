namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationDatabaseBindingMigrationItem(
    string TenantId,
    string AppCode,
    string Outcome,
    string? Message);
