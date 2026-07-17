using System.Text.Json;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataProviderCapabilityMatrixTests
{
    [Fact]
    public void Provider_matrix_matches_the_latest_provider_contract_and_fails_closed()
    {
        var root = RepositoryRoot();
        var path = Path.Combine(root, "docs", "low-code-refactor", "database-provider-capability-matrix.json");
        Assert.True(File.Exists(path), $"missing provider capability matrix: {path}");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var providers = document.RootElement.GetProperty("providers").EnumerateArray().ToArray();
        Assert.Equal(["SqlServer", "MySql", "PostgreSql", "Sqlite"], providers.Select(item => item.GetProperty("provider").GetString() ?? string.Empty).ToArray());

        var implementations = new IApplicationDataSourceProvider[]
        {
            new SqlServerApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqliteApplicationDataSourceProvider()
        };

        foreach (var (matrix, provider) in providers.Zip(implementations))
        {
            Assert.Equal(provider.Type, matrix.GetProperty("implementationType").GetString());
            Assert.Equal(provider.Capability.SupportsTransactionalDdl, matrix.GetProperty("supportsTransactionalDdl").GetBoolean());
            Assert.Equal(provider.Capability.SupportsAtomicViewReplace, matrix.GetProperty("supportsAtomicViewReplace").GetBoolean());
            Assert.Equal(provider.Capability.SupportsReturning, matrix.GetProperty("supportsReturning").GetBoolean());
            Assert.Equal(provider.Capability.SupportsCancellation, matrix.GetProperty("supportsCancellation").GetBoolean());
            Assert.Equal(provider.Capability.SupportsSchemas, matrix.GetProperty("supportsSchemas").GetBoolean());
            Assert.Equal(provider.Capability.MaxPageSize, matrix.GetProperty("maxPageSize").GetInt32());
            Assert.Equal("required", matrix.GetProperty("catalog").GetString(), ignoreCase: true);
            Assert.Equal("required", matrix.GetProperty("ddlPlan").GetString(), ignoreCase: true);
            Assert.Equal("required", matrix.GetProperty("typedEdit").GetString(), ignoreCase: true);
            Assert.Equal("required", matrix.GetProperty("queryCancel").GetString(), ignoreCase: true);
            Assert.Equal("required", matrix.GetProperty("audit").GetString(), ignoreCase: true);
            Assert.Equal("provider", matrix.GetProperty("identifierQuote").GetString(), ignoreCase: true);
        }

        var boundary = document.RootElement.GetProperty("boundary");
        Assert.Equal("Fail", boundary.GetProperty("unsafeDefault").GetString());
        Assert.Equal("Blocked", boundary.GetProperty("missingContainerOrCredential").GetString());
    }

    [Fact]
    public void Provider_catalog_contracts_are_complete_for_schema_and_security_metadata()
    {
        var providers = new IApplicationDataSourceProvider[]
        {
            new SqlServerApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqliteApplicationDataSourceProvider()
        };

        foreach (var provider in providers)
        {
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.TablesSql));
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.ColumnsSql));
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.ConstraintsSql));
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.IndexesSql));
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.TriggersSql));
            Assert.False(string.IsNullOrWhiteSpace(provider.Catalog.CommentsSql));
            Assert.InRange(provider.Capability.MaxWriteRows, 1, 1000);
            Assert.InRange(provider.Capability.MaxPreviewRows, 1, 200);
            Assert.True(provider.Capability.SupportsOriginalValueConcurrency);
        }
    }

    [Fact]
    public void Provider_join_capabilities_are_canonical_and_fail_closed()
    {
        var providers = new IApplicationDataSourceProvider[]
        {
            new SqlServerApplicationDataSourceProvider(),
            new MySqlApplicationDataSourceProvider(),
            new PostgreSqlApplicationDataSourceProvider(),
            new SqliteApplicationDataSourceProvider()
        };

        Assert.Equal([ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right, ApplicationQueryJoinType.Full], providers[0].Capability.SupportedJoinTypes);
        Assert.Equal([ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right], providers[1].Capability.SupportedJoinTypes);
        Assert.Equal([ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right, ApplicationQueryJoinType.Full], providers[2].Capability.SupportedJoinTypes);
        Assert.Equal([ApplicationQueryJoinType.Inner, ApplicationQueryJoinType.Left, ApplicationQueryJoinType.Right], providers[3].Capability.SupportedJoinTypes);
    }

    [Fact]
    public void Provider_support_level_exposes_mapping_cache_contract()
    {
        var capability = new SqliteApplicationDataSourceProvider().Capability;

        Assert.Equal(ApplicationDataSourceSupportLevel.Full, capability.SupportLevel);
        Assert.Contains(capability.FeatureSupport, item => item.Feature == "structuredMappingCache" && item.SupportLevel == ApplicationDataSourceSupportLevel.Full);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AsterERP.sln")))
            directory = directory.Parent;
        return Assert.IsType<DirectoryInfo>(directory).FullName;
    }
}
