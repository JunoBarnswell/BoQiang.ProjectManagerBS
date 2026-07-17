namespace AsterERP.Contracts.ApplicationConsole;

public static class ApplicationDatabaseBindingStatus
{
    public const string NotConfigured = "NotConfigured";
    public const string Ready = "Ready";
    public const string InvalidConfiguration = "InvalidConfiguration";
    public const string MigrationRequired = "MigrationRequired";
    public const string PermissionDenied = "PermissionDenied";
    public const string Unavailable = "Unavailable";
}
