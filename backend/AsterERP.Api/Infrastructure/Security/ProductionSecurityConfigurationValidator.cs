namespace AsterERP.Api.Infrastructure.Security;

public static class ProductionSecurityConfigurationValidator
{
    public static void Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsProduction())
        {
            return;
        }

        var allowedHosts = configuration["AllowedHosts"];
        if (string.IsNullOrWhiteSpace(allowedHosts) || allowedHosts == "*" || ContainsPlaceholder(allowedHosts))
        {
            throw new InvalidOperationException("Production requires an explicit AllowedHosts value.");
        }

        var frontendOrigin = configuration["Cors:FrontendOrigin"];
        if (!Uri.TryCreate(frontendOrigin, UriKind.Absolute, out var origin) ||
            !string.Equals(origin.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            IsLoopback(origin.Host) ||
            ContainsPlaceholder(frontendOrigin))
        {
            throw new InvalidOperationException("Production requires a non-loopback HTTPS Cors:FrontendOrigin.");
        }

        if (configuration.GetValue("SqlSugar:LogSql", false))
        {
            throw new InvalidOperationException("SqlSugar:LogSql must be false in Production.");
        }

        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath) || !Path.IsPathFullyQualified(keysPath) || ContainsPlaceholder(keysPath))
        {
            throw new InvalidOperationException("Production requires an explicit absolute DataProtection:KeysPath.");
        }
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);

    private static bool ContainsPlaceholder(string? value) =>
        value?.Contains("__SET_", StringComparison.OrdinalIgnoreCase) == true;
}
