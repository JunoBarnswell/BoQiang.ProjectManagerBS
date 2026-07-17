using AsterERP.Api.Application.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed record ApplicationDataSourceConnectionCapability(
    string Provider,
    IReadOnlyList<ApplicationDataSourceSslMode> SupportedSslModes,
    ApplicationDataSourceSslMode? DefaultSslMode,
    bool SupportsConnectionTimeout,
    bool SupportsPoolSize,
    bool SupportsCharset,
    string? DefaultCharset,
    int DefaultTimeoutSeconds = 15,
    int DefaultPoolSize = 20)
{
    public static ApplicationDataSourceConnectionCapability ForProvider(string provider) => provider switch
    {
        "MySql" => new(
            provider,
            [
                ApplicationDataSourceSslMode.Disabled,
                ApplicationDataSourceSslMode.Preferred,
                ApplicationDataSourceSslMode.Required,
                ApplicationDataSourceSslMode.VerifyCA,
                ApplicationDataSourceSslMode.VerifyFull
            ],
            ApplicationDataSourceSslMode.Preferred,
            true,
            true,
            true,
            "utf8mb4"),
        "PostgreSQL" => new(
            provider,
            [
                ApplicationDataSourceSslMode.Disabled,
                ApplicationDataSourceSslMode.Allow,
                ApplicationDataSourceSslMode.Preferred,
                ApplicationDataSourceSslMode.Required,
                ApplicationDataSourceSslMode.VerifyCA,
                ApplicationDataSourceSslMode.VerifyFull
            ],
            ApplicationDataSourceSslMode.Preferred,
            true,
            true,
            true,
            "UTF8"),
        "SqlServer" => new(provider, [], null, true, true, false, null),
        "Sqlite" => new(provider, [], null, false, false, false, null),
        _ => throw new ArgumentException($"Unknown data source provider: {provider}", nameof(provider))
    };
}
