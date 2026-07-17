namespace AsterERP.Api.Application.ApplicationConsole;

public sealed record ApplicationManagedSqliteDatabase(
    string DatabaseName,
    string AbsolutePath,
    string ConnectionString);
