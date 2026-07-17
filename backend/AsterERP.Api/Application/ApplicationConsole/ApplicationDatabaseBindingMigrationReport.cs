namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationDatabaseBindingMigrationReport(
    int Scanned,
    int Migrated,
    int AlreadyCanonical,
    int NotConfigured,
    int Failed,
    IReadOnlyList<ApplicationDatabaseBindingMigrationItem> Items);
