using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationDataCenterProviderTests
{
    [Theory]
    [InlineData("SELECT 1")]
    [InlineData("WITH x AS (SELECT 1 AS id) SELECT id FROM x")]
    [InlineData("SELECT 'update'")]
    public void ReadOnlyPolicyAcceptsSingleQueryStatements(string sql)
    {
        Assert.Equal(sql, ApplicationDataSourceSqlPolicy.RequireSelectSql(sql, "test"));
    }

    [Theory]
    [InlineData("SELECT 1; DROP TABLE orders")]
    [InlineData("WITH x AS (SELECT 1) UPDATE orders SET id = 2")]
    [InlineData("SELECT 1 /* DROP TABLE orders */")]
    public void ReadOnlyPolicyRejectsBatchAndCommentSmuggling(string sql)
    {
        Assert.Throws<ValidationException>(() => ApplicationDataSourceSqlPolicy.RequireSelectSql(sql, "test"));
    }

    public static IEnumerable<object[]> Providers()
    {
        yield return [new SqliteApplicationDataSourceProvider()];
        yield return [new MySqlApplicationDataSourceProvider()];
        yield return [new PostgreSqlApplicationDataSourceProvider()];
        yield return [new SqlServerApplicationDataSourceProvider()];
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderQuotesOnlyValidatedIdentifiers(IApplicationDataSourceProvider provider)
    {
        Assert.Contains("orders", provider.QuoteQualified("sales", "orders"));
        Assert.Throws<ValidationException>(() => provider.QuoteIdentifier("orders; DROP TABLE users"));
        Assert.Throws<ValidationException>(() => provider.QuoteIdentifier("users--"));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderClassifiesOnlySingleStatementReadQueries(IApplicationDataSourceProvider provider)
    {
        Assert.True(provider.IsReadOnlyStatement("SELECT id FROM orders"));
        Assert.True(provider.IsReadOnlyStatement("WITH recent AS (SELECT id FROM orders) SELECT id FROM recent"));
        Assert.True(provider.IsReadOnlyStatement("WITH recent (id) AS (SELECT id FROM orders) SELECT id FROM recent"));
        Assert.True(provider.IsReadOnlyStatement("WITH RECURSIVE recent AS (SELECT id FROM orders) SELECT id FROM recent"));
        Assert.False(provider.IsReadOnlyStatement("WITH doomed AS (SELECT id FROM orders) DELETE FROM orders RETURNING id"));
        Assert.False(provider.IsReadOnlyStatement("WITH changed AS (SELECT id FROM orders) UPDATE orders SET status = 'x'"));
        Assert.False(provider.IsReadOnlyStatement("SELECT id FROM orders; DELETE FROM orders"));
        Assert.False(provider.IsReadOnlyStatement("UPDATE orders SET status = 'x'"));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderBuildsBoundedPagination(IApplicationDataSourceProvider provider)
    {
        var sql = provider.BuildPageSql("SELECT * FROM \"orders\"", " ORDER BY \"id\"", 20, 50);

        Assert.Contains("50", sql, StringComparison.Ordinal);
        Assert.Contains("20", sql, StringComparison.Ordinal);
        Assert.Throws<ValidationException>(() => provider.BuildPageSql("SELECT 1", string.Empty, 0, 1001));
        Assert.Throws<ValidationException>(() => provider.BuildPageSql("SELECT 1; DROP TABLE orders", string.Empty, 0, 10));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderOwnsPreviewDdlAndTypedSqlDifferences(IApplicationDataSourceProvider provider)
    {
        var columns = new[]
        {
            new ApplicationDataSourceCreateTableColumnRequest("id", "INTEGER", false, true, null, null),
            new ApplicationDataSourceCreateTableColumnRequest("name", "TEXT", true, false, null, null)
        };

        var createTable = provider.BuildCreateTableSql("sales", "orders", columns);
        Assert.Contains("CREATE TABLE", createTable, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(provider.QuoteIdentifier("id"), createTable, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY", createTable, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(provider.QuoteIdentifier("orders"), provider.BuildPreviewSql("SELECT * FROM " + provider.QuoteQualified("sales", "orders"), 20), StringComparison.Ordinal);
        Assert.Contains("LIKE", provider.BuildTextSearchSql(provider.QuoteIdentifier("name"), "@keyword"), StringComparison.OrdinalIgnoreCase);
        Assert.Throws<ValidationException>(() => provider.BuildPreviewSql("SELECT 1; DROP TABLE orders", 20));
        Assert.Throws<ValidationException>(() => provider.BuildCreateOrReplaceViewSql("orders_view; DROP TABLE orders", "SELECT 1"));
        Assert.Throws<ValidationException>(() => provider.BuildDropViewSql("orders_view; DROP TABLE orders"));
    }

    [Fact]
    public void PreviewReaderDelegatesSqlServerDialectToProvider()
    {
        var sql = ApplicationDataPreviewReader.ResolvePreviewSql(
            "SELECT id FROM orders",
            null,
            10,
            new SqlServerApplicationDataSourceProvider());

        Assert.StartsWith("SELECT TOP 10", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" LIMIT ", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewReaderQuotesQualifiedTableThroughProvider()
    {
        var sql = ApplicationDataPreviewReader.ResolvePreviewSql(
            null,
            "dbo.orders",
            10,
            new SqlServerApplicationDataSourceProvider());

        Assert.Contains("[dbo].[orders]", sql, StringComparison.Ordinal);
        Assert.StartsWith("SELECT TOP 10", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PreviewReaderRejectsSchemaForProviderWithoutSchemaSupport()
    {
        Assert.Throws<ValidationException>(() => ApplicationDataPreviewReader.ResolvePreviewSql(
            null,
            "main.orders",
            10,
            new SqliteApplicationDataSourceProvider()));
    }

    [Fact]
    public void ProviderCapabilitiesExposeUniformSafetyLimits()
    {
        Assert.All(Providers().Select(item => (IApplicationDataSourceProvider)item[0]), provider =>
        {
            Assert.True(provider.Capability.MaxPageSize > 0);
            Assert.True(provider.Capability.MaxPreviewRows > 0);
            Assert.True(provider.Capability.MaxWriteRows > 0);
            Assert.True(provider.Capability.SupportsOriginalValueConcurrency);
        });
    }

    [Fact]
    public void SqlServerUsesNativeViewReplacementSyntax()
    {
        var provider = new SqlServerApplicationDataSourceProvider();

        var sql = provider.BuildCreateOrReplaceViewSql("[dbo].[orders_view]", "SELECT 1");

        Assert.StartsWith("CREATE OR ALTER VIEW", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE OR REPLACE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MySqlUsesCharForTextSearchCasting()
    {
        var provider = new MySqlApplicationDataSourceProvider();

        var sql = provider.BuildTextSearchSql(provider.QuoteIdentifier("name"), "@keyword");

        Assert.Contains("CAST(`name` AS CHAR)", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AS TEXT", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SqliteRejectsAtomicViewReplacementInsteadOfEmittingUnsupportedSql()
    {
        var provider = new SqliteApplicationDataSourceProvider();

        Assert.Throws<ValidationException>(() => provider.BuildCreateOrReplaceViewSql("\"orders_view\"", "SELECT 1"));
    }

    [Theory]
    [InlineData("TEXT; DROP TABLE users")]
    [InlineData("TEXT DROP TABLE users")]
    public void ProviderRejectsDdlTypeFragmentsThatContainSqlStatements(string dataType)
    {
        var provider = new SqliteApplicationDataSourceProvider();
        var columns = new[] { new ApplicationDataSourceCreateTableColumnRequest("value", dataType, true, false, null, null) };

        Assert.Throws<ValidationException>(() => provider.BuildCreateTableSql(null, "items", columns));
    }

    [Fact]
    public void ProviderRejectsDdlDefaultsThatContainCommentsOrStatements()
    {
        var provider = new SqliteApplicationDataSourceProvider();
        var columns = new[] { new ApplicationDataSourceCreateTableColumnRequest("value", "TEXT", true, false, "'ok' --", null) };

        Assert.Throws<ValidationException>(() => provider.BuildCreateTableSql(null, "items", columns));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderRendersOnlyTypedDefaultExpressions(IApplicationDataSourceProvider provider)
    {
        var columns = new[]
        {
            new ApplicationDataSourceCreateTableColumnRequest("amount", "INTEGER", true, false, "42", null),
            new ApplicationDataSourceCreateTableColumnRequest("enabled", "INTEGER", true, false, "true", null),
            new ApplicationDataSourceCreateTableColumnRequest("label", "TEXT", true, false, "'ok'", null),
            new ApplicationDataSourceCreateTableColumnRequest("message", "TEXT", true, false, "'foo--bar;baz'", null),
            new ApplicationDataSourceCreateTableColumnRequest("created_on", "TEXT", true, false, "'2026-07-14'", null),
            new ApplicationDataSourceCreateTableColumnRequest("missing", "TEXT", true, false, "NULL", null),
            new ApplicationDataSourceCreateTableColumnRequest("created_at", "TEXT", true, false, "CURRENT_TIMESTAMP", null)
        };

        var sql = provider.BuildCreateTableSql(null, "items", columns);

        Assert.Contains("DEFAULT 42", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT 'ok'", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT 'foo--bar;baz'", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT '2026-07-14'", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("DEFAULT CURRENT_TIMESTAMP", sql, StringComparison.Ordinal);
        Assert.Contains(provider.Type.Equals("SqlServer", StringComparison.OrdinalIgnoreCase) ? "DEFAULT 1" : "DEFAULT TRUE", sql, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderRejectsUnsafeAlterDefaultsBeforeSqlGeneration(IApplicationDataSourceProvider provider)
    {
        var current = new[]
        {
            new ApplicationDataSourceCreateTableColumnRequest("value", "TEXT", true, false, null, null)
        };
        var unsafeDefaults = new[] { "0; DROP TABLE users", "'ok' --", "0 /* comment */", "SELECT 1" };

        foreach (var unsafeDefault in unsafeDefaults)
        {
            var desired = new[]
            {
                new ApplicationDataSourceCreateTableColumnRequest("value", "TEXT", true, false, unsafeDefault, null)
            };

            Assert.Throws<ValidationException>(() => provider.BuildAlterTableSql(null, "items", current, desired));
        }
    }

    [Theory]
    [InlineData("Sqlite", "NOW()")]
    [InlineData("SqlServer", "NOW()")]
    public void ProviderRejectsFunctionsOutsideItsDefaultWhitelist(string providerType, string expression)
    {
        var provider = Providers()
            .Select(item => (IApplicationDataSourceProvider)item[0])
            .Single(item => item.Type.Equals(providerType, StringComparison.OrdinalIgnoreCase));
        var columns = new[] { new ApplicationDataSourceCreateTableColumnRequest("created_at", "TEXT", true, false, expression, null) };

        Assert.Throws<ValidationException>(() => provider.BuildCreateTableSql(null, "items", columns));
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void ProviderBuildsAlterPlansWithoutImplicitPrimaryKeyRebuild(IApplicationDataSourceProvider provider)
    {
        var current = new[]
        {
            new ApplicationDataSourceCreateTableColumnRequest("id", "INTEGER", false, true, null, null),
            new ApplicationDataSourceCreateTableColumnRequest("name", "TEXT", true, false, null, null)
        };
        var desired = new[]
        {
            current[0],
            current[1],
            new ApplicationDataSourceCreateTableColumnRequest("created_at", "TEXT", true, false, null, null)
        };

        var statements = provider.BuildAlterTableSql(null, "orders", current, desired);

        Assert.Single(statements);
        Assert.Contains("ALTER TABLE", statements[0], StringComparison.OrdinalIgnoreCase);
        Assert.Contains(provider.QuoteIdentifier("created_at"), statements[0], StringComparison.Ordinal);
        Assert.Throws<ValidationException>(() => provider.BuildAlterTableSql(
            null,
            "orders",
            current,
            [new("id", "INTEGER", false, false, null, null), current[1]]));
    }
}
