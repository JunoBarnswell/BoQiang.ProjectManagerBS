using AsterERP.Api.Application.ApplicationConsole;
using Microsoft.Extensions.Logging.Abstractions;
using SqlSugar;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataSourceExternalProviderGateTests
{
    private static readonly (string Provider, string EnvironmentVariable)[] ProviderConfigurations =
    [
        ("SqlServer", "ASTERERP_TEST_SQLSERVER_CONNECTION"),
        ("MySql", "ASTERERP_TEST_MYSQL_CONNECTION"),
        ("PostgreSQL", "ASTERERP_TEST_POSTGRESQL_CONNECTION"),
        ("Sqlite", "ASTERERP_TEST_SQLITE_CONNECTION")
    ];

    [Fact]
    public async Task Real_provider_gate_executes_connection_ddl_and_dml_or_records_blocked()
    {
        foreach (var configuration in ProviderConfigurations)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(configuration.EnvironmentVariable)))
            {
                Console.WriteLine($"BLOCKED provider={configuration.Provider}; reason=environment variable {configuration.EnvironmentVariable} is not configured; no real container credential was supplied; required=real-container-and-authorized-credential");
                continue;
            }

            await ExecuteProviderProbeAsync(configuration.Provider, configuration.EnvironmentVariable);
        }
    }

    private static async Task ExecuteProviderProbeAsync(string provider, string connectionEnvironmentVariable)
    {
        var connectionString = Environment.GetEnvironmentVariable(connectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            RecordBlocked(provider, $"environment variable {connectionEnvironmentVariable} is not configured; no real container credential was supplied");
            return;
        }

        var factory = new ApplicationDatabaseConnectionFactory(NullLogger<ApplicationDatabaseConnectionFactory>.Instance);
        using var client = factory.Create(new ApplicationDatabaseBindingOptions(provider, connectionString, "external-provider-gate", null));
        var objectName = $"astererp_gate_{Guid.NewGuid():N}";
        Exception? cleanupFailure = null;

        try
        {
            await client.Ado.GetIntAsync("SELECT 1");
            await client.Ado.ExecuteCommandAsync(BuildCreateTableSql(provider, objectName));
            await client.Ado.ExecuteCommandAsync(BuildInsertSql(provider, objectName));
            var count = await client.Ado.GetIntAsync(BuildCountSql(provider, objectName));
            Assert.Equal(1, count);
        }
        catch (Exception exception)
        {
            RecordBlocked(provider, $"real provider connection or DDL/DML probe failed: {exception.GetType().Name}: {Sanitize(exception.Message)}");
            return;
        }
        finally
        {
            try
            {
                await client.Ado.ExecuteCommandAsync(BuildDropTableSql(provider, objectName));
            }
            catch (Exception exception)
            {
                cleanupFailure = exception;
            }
        }

        if (cleanupFailure is not null)
            RecordBlocked(provider, $"cleanup failed: {cleanupFailure.GetType().Name}: {Sanitize(cleanupFailure.Message)}");
    }

    private static void RecordBlocked(string provider, string reason)
    {
        var evidence = $"BLOCKED provider={provider}; reason={reason}; required=real-container-and-authorized-credential";
        Console.WriteLine(evidence);
        throw new InvalidOperationException(evidence);
    }

    private static string BuildCreateTableSql(string provider, string table) => provider switch
    {
        "SqlServer" => $"CREATE TABLE [{table}] (id INT NOT NULL PRIMARY KEY, marker NVARCHAR(32) NOT NULL)",
        "MySql" => $"CREATE TEMPORARY TABLE `{table}` (id INT NOT NULL PRIMARY KEY, marker VARCHAR(32) NOT NULL)",
        "PostgreSQL" => $"CREATE TEMP TABLE \"{table}\" (id INTEGER NOT NULL PRIMARY KEY, marker VARCHAR(32) NOT NULL)",
        "Sqlite" => $"CREATE TABLE \"{table}\" (id INTEGER NOT NULL PRIMARY KEY, marker TEXT NOT NULL)",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static string BuildInsertSql(string provider, string table) => provider switch
    {
        "SqlServer" => $"INSERT INTO [{table}] (id, marker) VALUES (1, 'provider-gate')",
        "MySql" => $"INSERT INTO `{table}` (id, marker) VALUES (1, 'provider-gate')",
        "PostgreSQL" or "Sqlite" => $"INSERT INTO \"{table}\" (id, marker) VALUES (1, 'provider-gate')",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static string BuildCountSql(string provider, string table) => provider switch
    {
        "SqlServer" => $"SELECT COUNT(1) FROM [{table}]",
        "MySql" => $"SELECT COUNT(1) FROM `{table}`",
        "PostgreSQL" or "Sqlite" => $"SELECT COUNT(1) FROM \"{table}\"",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static string BuildDropTableSql(string provider, string table) => provider switch
    {
        "SqlServer" => $"IF OBJECT_ID(N'[{table}]', N'U') IS NOT NULL DROP TABLE [{table}]",
        "MySql" => $"DROP TEMPORARY TABLE IF EXISTS `{table}`",
        "PostgreSQL" => $"DROP TABLE IF EXISTS \"{table}\"",
        "Sqlite" => $"DROP TABLE IF EXISTS \"{table}\"",
        _ => throw new ArgumentOutOfRangeException(nameof(provider))
    };

    private static string Sanitize(string message) =>
        message.Replace(Environment.NewLine, " ", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
