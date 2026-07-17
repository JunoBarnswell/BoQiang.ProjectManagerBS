using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Modules.ApplicationDataCenter;
using AsterERP.Api.Tests.Support;
using AsterERP.Shared.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceConnectionFactoryTests
{
    [Fact]
    public void Provider_security_defaults_are_strict_and_explicit_options_are_preserved()
    {
        var factory = CreateFactory();

        var mySql = factory.BuildConnectionString(CreateOptions("MySql"), DbType.MySql);
        var postgres = factory.BuildConnectionString(CreateOptions("PostgreSql"), DbType.PostgreSQL);
        var sqlServer = factory.BuildConnectionString(CreateOptions("SqlServer"), DbType.SqlServer);
        var relaxedSqlServer = factory.BuildConnectionString(CreateOptions("SqlServer") with { Encrypt = false, TrustServerCertificate = true }, DbType.SqlServer);

        Assert.Contains("SslMode=Preferred", mySql, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Prefer", postgres, StringComparison.Ordinal);
        Assert.Contains("Connection Timeout=15", mySql, StringComparison.Ordinal);
        Assert.Contains("Maximum Pool Size=20", mySql, StringComparison.Ordinal);
        Assert.Contains("Character Set=utf8mb4", mySql, StringComparison.Ordinal);
        Assert.Contains("Timeout=15", postgres, StringComparison.Ordinal);
        Assert.Contains("Maximum Pool Size=20", postgres, StringComparison.Ordinal);
        Assert.Contains("Client Encoding=UTF8", postgres, StringComparison.Ordinal);
        Assert.Contains("Connect Timeout=15", sqlServer, StringComparison.Ordinal);
        Assert.Contains("Max Pool Size=20", sqlServer, StringComparison.Ordinal);
        Assert.Contains("Encrypt=True", sqlServer, StringComparison.Ordinal);
        Assert.Contains("TrustServerCertificate=False", sqlServer, StringComparison.Ordinal);
        Assert.Contains("Encrypt=False", relaxedSqlServer, StringComparison.Ordinal);
        Assert.Contains("TrustServerCertificate=True", relaxedSqlServer, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_security_modes_reject_unknown_values()
    {
        var factory = CreateFactory();

        Assert.Throws<ValidationException>(() => factory.BuildConnectionString(CreateOptions("MySql") with { SslMode = "Prefer" }, DbType.MySql));
        Assert.Throws<ValidationException>(() => factory.BuildConnectionString(CreateOptions("PostgreSql") with { SslMode = "PreferredLegacy" }, DbType.PostgreSQL));
        Assert.Throws<ValidationException>(() => factory.BuildConnectionString(CreateOptions("SqlServer") with { SslMode = "Required" }, DbType.SqlServer));
        Assert.Throws<ValidationException>(() => factory.BuildConnectionString(CreateOptions("SqlServer") with { Charset = "UTF8" }, DbType.SqlServer));
        Assert.Throws<ValidationException>(() => factory.BuildConnectionString(CreateOptions("MySql") with { TimeoutSeconds = 301 }, DbType.MySql));
    }

    [Fact]
    public void Explicit_connection_options_are_consumed_for_each_supported_provider()
    {
        var factory = CreateFactory();

        var mySql = factory.BuildConnectionString(CreateOptions("MySql") with { TimeoutSeconds = 42, PoolSize = 48, Charset = "latin1", SslMode = "VerifyCA" }, DbType.MySql);
        var postgres = factory.BuildConnectionString(CreateOptions("PostgreSql") with { TimeoutSeconds = 43, PoolSize = 49, Charset = "UTF8", SslMode = "VerifyFull" }, DbType.PostgreSQL);
        var sqlServer = factory.BuildConnectionString(CreateOptions("SqlServer") with { TimeoutSeconds = 44, PoolSize = 50 }, DbType.SqlServer);

        Assert.Contains("Connection Timeout=42", mySql, StringComparison.Ordinal);
        Assert.Contains("Maximum Pool Size=48", mySql, StringComparison.Ordinal);
        Assert.Contains("Character Set=latin1", mySql, StringComparison.Ordinal);
        Assert.Contains("SslMode=VerifyCA", mySql, StringComparison.Ordinal);
        Assert.Contains("Timeout=43", postgres, StringComparison.Ordinal);
        Assert.Contains("Maximum Pool Size=49", postgres, StringComparison.Ordinal);
        Assert.Contains("Client Encoding=UTF8", postgres, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=VerifyFull", postgres, StringComparison.Ordinal);
        Assert.Contains("Connect Timeout=44", sqlServer, StringComparison.Ordinal);
        Assert.Contains("Max Pool Size=50", sqlServer, StringComparison.Ordinal);
    }

    [Fact]
    public void Explicit_connection_string_is_updated_with_retained_options()
    {
        var factory = CreateFactory();
        var options = CreateOptions("PostgreSql") with
        {
            ConnectionString = "Host=localhost;Database=astererp;Username=user;Password=password",
            TimeoutSeconds = 60,
            PoolSize = 70,
            Charset = "UTF8",
            SslMode = "Preferred"
        };

        var connectionString = factory.BuildConnectionString(options, DbType.PostgreSQL);

        Assert.Contains("Timeout=60", connectionString, StringComparison.Ordinal);
        Assert.Contains("Maximum Pool Size=70", connectionString, StringComparison.Ordinal);
        Assert.Contains("Client Encoding=UTF8", connectionString, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Prefer", connectionString, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_reads_retained_connection_fields_from_public_and_secret_config()
    {
        var factory = CreateFactory();
        var entity = new ApplicationDataSourceEntity
        {
            ObjectType = "MySql",
            ConfigJson = "{\"host\":\"localhost\",\"timeoutSeconds\":42,\"poolSize\":48,\"charset\":\"latin1\",\"sslMode\":\"Required\"}",
            SecretConfigCipherText = "{\"user\":\"user\",\"password\":\"password\"}"
        };

        var options = factory.Resolve(entity);

        Assert.Equal(42, options.TimeoutSeconds);
        Assert.Equal(48, options.PoolSize);
        Assert.Equal("latin1", options.Charset);
        Assert.Equal("Required", options.SslMode);
        Assert.Equal("user", options.UserName);
    }

    [Fact]
    public async Task Database_client_creation_honors_cancellation_before_provider_resolution()
    {
        var factory = CreateFactory();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var entity = new ApplicationDataSourceEntity
        {
            Id = "cancelled-source",
            ObjectType = "MySql",
            ConfigJson = "{\"host\":\"localhost\",\"database\":\"db\",\"user\":\"user\"}"
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => factory.CreateDatabaseClientAsync(entity, cancellation.Token));
    }

    private static ApplicationDataSourceConnectionFactory CreateFactory() =>
        new(
            new TestHostEnvironment(AppContext.BaseDirectory),
            new NoopSecretProtector(),
            new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance));

    private static ApplicationDataSourceConnectionOptions CreateOptions(string type) =>
        new(type, null, null, null, "localhost", null, "astererp", "user", "password", null, null);

    private sealed class NoopSecretProtector : IApplicationDataSecretProtector
    {
        public string Protect(string plainText) => plainText;
        public string Unprotect(string cipherText) => cipherText;
        public string BuildPublicSecretSummary(string? cipherText) => "{}";
        public string BuildPublicSecretSummary(string? cipherText, string secretRef, DateTime? updatedAt) => "{}";
    }
}
