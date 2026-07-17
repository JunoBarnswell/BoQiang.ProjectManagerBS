using System.Text.Json.Nodes;
using AsterERP.Api.Application.ApplicationConsole;
using AsterERP.Api.Infrastructure.Security;
using AsterERP.Api.Tests.Support;
using AsterERP.Contracts.ApplicationConsole;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDatabaseBindingResolverTests : IDisposable
{
    private readonly string _contentRootPath = Path.Combine(Path.GetTempPath(), $"astererp-binding-resolver-{Guid.NewGuid():N}");

    public ApplicationDatabaseBindingResolverTests()
    {
        Directory.CreateDirectory(_contentRootPath);
    }

    [Fact]
    public void Resolve_reads_primary_application_database_node()
    {
        var resolver = CreateResolver();
        var configJson = resolver.Merge(
            null,
            new ApplicationDatabaseBindingOptions("Sqlite", "Data Source=mes.db", "客户A MES 应用库", "mes.db", DateTime.UtcNow, "admin"),
            "admin",
            DateTime.UtcNow);

        var binding = resolver.Resolve(configJson);

        Assert.NotNull(binding);
        Assert.Equal("Sqlite", binding.Provider);
        Assert.Equal("Data Source=mes.db", binding.ConnectionString);
        Assert.Equal("客户A MES 应用库", binding.DisplayName);
        Assert.Equal("mes.db", binding.DatabaseName);
    }

    [Fact]
    public void ResolveStatus_marks_legacy_database_node_as_migration_required()
    {
        var resolver = CreateResolver();
        var primaryJson = resolver.Merge(
            null,
            new ApplicationDatabaseBindingOptions("Sqlite", "Data Source=legacy.db", "Legacy MES", "legacy.db", DateTime.UtcNow, "admin"),
            "admin",
            DateTime.UtcNow);
        var root = JsonNode.Parse(primaryJson)!.AsObject();
        root["database"] = root["applicationDatabase"]!.DeepClone();
        root.Remove("applicationDatabase");

        var resolution = resolver.ResolveStatus(root.ToJsonString());

        Assert.Equal(ApplicationDatabaseBindingStatus.MigrationRequired, resolution.Status);
        Assert.Null(resolution.Options);
        Assert.Throws<ValidationException>(() => resolver.Resolve(root.ToJsonString()));
    }

    [Fact]
    public void ResolveStatus_distinguishes_invalid_json_from_not_configured()
    {
        var resolver = CreateResolver();

        var resolution = resolver.ResolveStatus("{invalid-json");

        Assert.Equal(ApplicationDatabaseBindingStatus.InvalidConfiguration, resolution.Status);
        Assert.Throws<ValidationException>(() => resolver.Resolve("{invalid-json"));
        Assert.False(resolver.HasBinding("{invalid-json"));
    }

    [Fact]
    public void ResolveStatus_rejects_primary_provider_alias_and_raw_connection_string()
    {
        var resolver = CreateResolver();

        var resolution = resolver.ResolveStatus("""
        {"applicationDatabase":{"provider":"sqlite3","connectionString":"Data Source=legacy.db"}}
        """);

        Assert.Equal(ApplicationDatabaseBindingStatus.MigrationRequired, resolution.Status);
        Assert.True(resolution.IsLegacy);
    }

    [Fact]
    public void CreateOptions_rejects_provider_aliases_for_new_bindings()
    {
        var resolver = CreateResolver();

        Assert.Throws<ValidationException>(() => resolver.CreateOptions(
            new ApplicationDatabaseBindingRequest("sqlite3", null, "mes.db", null),
            "tenant-a",
            "MES"));
    }

    [Fact]
    public void ResolveStatus_reports_decryption_failure_as_invalid_configuration()
    {
        var resolver = CreateResolver();

        var resolution = resolver.ResolveStatus("""
        {"applicationDatabase":{"provider":"Sqlite","connectionStringCipherText":"not-a-valid-secret"}}
        """);

        Assert.Equal(ApplicationDatabaseBindingStatus.InvalidConfiguration, resolution.Status);
        Assert.Contains("密文", resolution.Message);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_contentRootPath))
            {
                Directory.Delete(_contentRootPath, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private ApplicationDatabaseBindingResolver CreateResolver()
    {
        var protector = new ApplicationConnectionStringProtector(DataProtectionProvider.Create(_contentRootPath));
        var sqliteResolver = new ApplicationManagedSqliteDatabaseResolver(new TestHostEnvironment(_contentRootPath));
        return new ApplicationDatabaseBindingResolver(protector, sqliteResolver);
    }
}
