using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataSourceConnectionFactory
{
    private readonly IApplicationDataSecretProtector secretProtector;
    private readonly IApplicationDatabaseConnectionFactory applicationDatabaseConnectionFactory;
    private readonly ApplicationDataSourceSqliteSandbox sqliteSandbox;

    [ActivatorUtilitiesConstructor]
    public ApplicationDataSourceConnectionFactory(
        IApplicationDataSecretProtector secretProtector,
        IApplicationDatabaseConnectionFactory applicationDatabaseConnectionFactory,
        ApplicationDataSourceSqliteSandbox sqliteSandbox)
    {
        this.secretProtector = secretProtector;
        this.applicationDatabaseConnectionFactory = applicationDatabaseConnectionFactory;
        this.sqliteSandbox = sqliteSandbox;
    }

    internal ApplicationDataSourceConnectionFactory(
        IHostEnvironment environment,
        IApplicationDataSecretProtector secretProtector,
        IApplicationDatabaseConnectionFactory applicationDatabaseConnectionFactory)
        : this(secretProtector, applicationDatabaseConnectionFactory, new ApplicationDataSourceSqliteSandbox(environment))
    {
    }

    public ApplicationDataSourceConnectionOptions Resolve(ApplicationDataSourceEntity entity)
    {
        var config = ApplicationDataCenterJson.DeserializeDictionary(entity.ConfigJson);
        var secret = string.IsNullOrWhiteSpace(entity.SecretConfigCipherText)
            ? []
            : ApplicationDataCenterJson.DeserializeDictionary(secretProtector.Unprotect(entity.SecretConfigCipherText));

        return new ApplicationDataSourceConnectionOptions(
            entity.ObjectType,
            null,
            Read(config, secret, "filePath") ?? Read(config, secret, "databaseName"),
            null,
            Read(config, secret, "host"),
            ReadInt(config, secret, "port"),
            Read(config, secret, "database"),
            Read(config, secret, "user"),
            Read(config, secret, "password"),
            null,
            null,
            Read(config, secret, "sslMode"),
            ReadBoolean(config, secret, "encrypt"),
            ReadBoolean(config, secret, "trustServerCertificate"),
            ReadInt(config, secret, "timeoutSeconds"),
            ReadInt(config, secret, "poolSize"),
            Read(config, secret, "charset"));
    }

    public ISqlSugarClient CreateDatabaseClient(ApplicationDataSourceEntity entity)
    {
        var options = Resolve(entity);
        var dbType = ResolveDbType(options.Type);
        return applicationDatabaseConnectionFactory.Create(new ApplicationDatabaseBindingOptions(
            ResolveProviderName(dbType), BuildConnectionString(options, dbType), entity.ObjectName, options.Database));
    }

    public async Task<ISqlSugarClient> CreateDatabaseClientAsync(
        ApplicationDataSourceEntity entity,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = Resolve(entity);
        var dbType = ResolveDbType(options.Type);
        var connectionString = await BuildConnectionStringAsync(options, dbType, entity.Id, cancellationToken);
        return applicationDatabaseConnectionFactory.Create(new ApplicationDatabaseBindingOptions(
            ResolveProviderName(dbType), connectionString, entity.ObjectName, options.Database));
    }

    public string BuildConnectionString(ApplicationDataSourceConnectionOptions options, DbType dbType)
    {
        var capability = ApplicationDataSourceConnectionCapability.ForProvider(ResolveProviderName(dbType));
        var sslMode = ResolveSslMode(options.SslMode, capability);
        var timeoutSeconds = ResolveConnectionOption(options.TimeoutSeconds, capability.SupportsConnectionTimeout, capability.DefaultTimeoutSeconds, 300, nameof(options.TimeoutSeconds));
        var poolSize = ResolveConnectionOption(options.PoolSize, capability.SupportsPoolSize, capability.DefaultPoolSize, 1000, nameof(options.PoolSize));
        var charset = ResolveCharset(options.Charset, capability);

        return dbType switch
        {
            DbType.Sqlite => BuildSqliteConnectionString(options),
            DbType.MySql => $"server={Required(options.Host, nameof(options.Host))};port={options.Port ?? 3306};database={Required(options.Database, nameof(options.Database))};uid={Required(options.UserName, nameof(options.UserName))};pwd={options.Password ?? string.Empty};Character Set={charset};Connection Timeout={timeoutSeconds};Maximum Pool Size={poolSize};SslMode={ToMySqlSslMode(sslMode)};",
            DbType.PostgreSQL => $"Host={Required(options.Host, nameof(options.Host))};Port={options.Port ?? 5432};Database={Required(options.Database, nameof(options.Database))};Username={Required(options.UserName, nameof(options.UserName))};Password={options.Password ?? string.Empty};Client Encoding={charset};Timeout={timeoutSeconds};Pooling=true;Maximum Pool Size={poolSize};SSL Mode={ToPostgreSqlSslMode(sslMode)};",
            DbType.SqlServer => $"Server={Required(options.Host, nameof(options.Host))},{options.Port ?? 1433};Database={Required(options.Database, nameof(options.Database))};User Id={Required(options.UserName, nameof(options.UserName))};Password={options.Password ?? string.Empty};Connect Timeout={timeoutSeconds};Max Pool Size={poolSize};Encrypt={options.Encrypt ?? true};TrustServerCertificate={options.TrustServerCertificate ?? false};",
            _ => throw new ValidationException("unsupported database provider", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };
    }

    public async Task<string> BuildConnectionStringAsync(
        ApplicationDataSourceConnectionOptions options,
        DbType dbType,
        string dataSourceId,
        CancellationToken cancellationToken = default)
    {
        if (dbType != DbType.Sqlite)
            return BuildConnectionString(options, dbType);

        ValidateConnectionOptions(options, ApplicationDataSourceConnectionCapability.ForProvider("Sqlite"));

        var path = await sqliteSandbox.ResolveAsync(
            Required(options.FilePath, nameof(options.FilePath)),
            dataSourceId,
            cancellationToken);
        return $"Data Source={path}";
    }

    public static bool IsDatabaseType(string type) =>
        string.Equals(type, ApplicationDataSourceType.Sqlite, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ApplicationDataSourceType.MySql, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ApplicationDataSourceType.PostgreSql, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ApplicationDataSourceType.SqlServer, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(type, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase);

    private static DbType ResolveDbType(string type) =>
        type switch
        {
            var value when string.Equals(value, ApplicationDataSourceType.Sqlite, StringComparison.OrdinalIgnoreCase) || string.Equals(value, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase) => DbType.Sqlite,
            var value when string.Equals(value, ApplicationDataSourceType.MySql, StringComparison.OrdinalIgnoreCase) => DbType.MySql,
            var value when string.Equals(value, ApplicationDataSourceType.PostgreSql, StringComparison.OrdinalIgnoreCase) => DbType.PostgreSQL,
            var value when string.Equals(value, ApplicationDataSourceType.SqlServer, StringComparison.OrdinalIgnoreCase) => DbType.SqlServer,
            _ => throw new ValidationException("unsupported database provider", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

    private static string ResolveProviderName(DbType dbType) => dbType switch
    {
        DbType.Sqlite => "Sqlite",
        DbType.MySql => "MySql",
        DbType.PostgreSQL => "PostgreSQL",
        DbType.SqlServer => "SqlServer",
        _ => throw new ValidationException("unsupported database provider", ErrorCodes.ApplicationDataCenterInvalidConfig)
    };

    private string BuildSqliteConnectionString(ApplicationDataSourceConnectionOptions options) =>
        $"Data Source={sqliteSandbox.Resolve(Required(options.FilePath, nameof(options.FilePath)))}";

    private static string Required(string? value, string message) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ValidationException(message, ErrorCodes.ApplicationDataCenterInvalidConfig)
            : value.Trim();

    private static string? Read(IReadOnlyDictionary<string, object?> config, IReadOnlyDictionary<string, object?> secret, string key) =>
        secret.TryGetValue(key, out var secretValue) && secretValue is not null
            ? secretValue.ToString()
            : config.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static int? ReadInt(IReadOnlyDictionary<string, object?> config, IReadOnlyDictionary<string, object?> secret, string key)
    {
        var value = Read(config, secret, key);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw new ValidationException($"{key} must be an integer", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static bool? ReadBoolean(IReadOnlyDictionary<string, object?> config, IReadOnlyDictionary<string, object?> secret, string key) =>
        bool.TryParse(Read(config, secret, key), out var parsed) ? parsed : null;

    private static ApplicationDataSourceSslMode? ResolveSslMode(
        string? value,
        ApplicationDataSourceConnectionCapability capability)
    {
        if (string.IsNullOrWhiteSpace(value))
            return capability.DefaultSslMode;

        var parsed = capability.SupportedSslModes.FirstOrDefault(item => string.Equals(item.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase));
        if (capability.DefaultSslMode is null || !capability.SupportedSslModes.Any(item => string.Equals(item.ToString(), value.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ValidationException($"SSL mode is not supported by {capability.Provider}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return parsed;
    }

    private static int ResolveConnectionOption(int? value, bool supported, int defaultValue, int maximum, string name)
    {
        if (!supported && value is not null)
            throw new ValidationException($"{name} is not supported by this provider", ErrorCodes.ApplicationDataCenterInvalidConfig);

        if (value.HasValue && (value.Value <= 0 || value.Value > maximum))
            throw new ValidationException($"{name} must be between 1 and {maximum}", ErrorCodes.ApplicationDataCenterInvalidConfig);

        return value ?? defaultValue;
    }

    private static string? ResolveCharset(string? value, ApplicationDataSourceConnectionCapability capability)
    {
        if (!capability.SupportsCharset)
        {
            if (!string.IsNullOrWhiteSpace(value))
                throw new ValidationException($"charset is not supported by {capability.Provider}", ErrorCodes.ApplicationDataCenterInvalidConfig);
            return null;
        }

        var charset = string.IsNullOrWhiteSpace(value) ? capability.DefaultCharset : value.Trim();
        if (string.IsNullOrWhiteSpace(charset) || charset.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '-' or '.')))
            throw new ValidationException("charset is invalid", ErrorCodes.ApplicationDataCenterInvalidConfig);

        return charset;
    }

    private static void ValidateConnectionOptions(
        ApplicationDataSourceConnectionOptions options,
        ApplicationDataSourceConnectionCapability capability)
    {
        _ = ResolveSslMode(options.SslMode, capability);
        _ = ResolveConnectionOption(options.TimeoutSeconds, capability.SupportsConnectionTimeout, capability.DefaultTimeoutSeconds, 300, nameof(options.TimeoutSeconds));
        _ = ResolveConnectionOption(options.PoolSize, capability.SupportsPoolSize, capability.DefaultPoolSize, 1000, nameof(options.PoolSize));
        _ = ResolveCharset(options.Charset, capability);
    }

    private static string ToMySqlSslMode(ApplicationDataSourceSslMode? mode) => mode switch
    {
        ApplicationDataSourceSslMode.Disabled => "Disabled",
        ApplicationDataSourceSslMode.Preferred => "Preferred",
        ApplicationDataSourceSslMode.Required => "Required",
        ApplicationDataSourceSslMode.VerifyCA => "VerifyCA",
        ApplicationDataSourceSslMode.VerifyFull => "VerifyFull",
        _ => throw new ValidationException("MySQL SSL mode is required", ErrorCodes.ApplicationDataCenterInvalidConfig)
    };

    private static string ToPostgreSqlSslMode(ApplicationDataSourceSslMode? mode) => mode switch
    {
        ApplicationDataSourceSslMode.Disabled => "Disable",
        ApplicationDataSourceSslMode.Allow => "Allow",
        ApplicationDataSourceSslMode.Preferred => "Prefer",
        ApplicationDataSourceSslMode.Required => "Require",
        ApplicationDataSourceSslMode.VerifyCA => "VerifyCA",
        ApplicationDataSourceSslMode.VerifyFull => "VerifyFull",
        _ => throw new ValidationException("PostgreSQL SSL mode is required", ErrorCodes.ApplicationDataCenterInvalidConfig)
    };
}
