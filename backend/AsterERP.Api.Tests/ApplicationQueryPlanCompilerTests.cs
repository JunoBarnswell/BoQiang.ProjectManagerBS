using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.ApplicationDataCenter.Providers;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationQueryPlanCompilerTests
{
    private const string OrdersNodeId = "orders-node";
    private const string CustomerNodeId = "customer-node";
    private const string OrdersResourceId = "data:table:orders";
    private const string CustomersResourceId = "data:table:customers";
    private const string OrdersId = "data:table:orders:column:id";
    private const string OrdersName = "data:table:orders:column:name";
    private const string OrdersStatus = "data:table:orders:column:status";
    private const string OrdersAmount = "data:table:orders:column:amount";
    private const string CustomerId = "data:table:customers:column:id";
    private const string CustomerCount = "data:table:customers:column:count";
    private const string StatusParameter = "data:parameter:status";
    private const string MinimumParameter = "data:parameter:minimum";
    private const string NameParameter = "data:parameter:name";
    private const string IdParameter = "data:parameter:id";

    private static readonly ApplicationQueryPlanCompiler Compiler = new();

    [Fact]
    public void CompilesTypedPlanWithBoundParametersAndProviderPagination()
    {
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId), new(OrdersName, "displayName", OrdersNodeId)],
            Filters = [new(OrdersStatus, "eq", StatusParameter, OrdersNodeId)],
            Sorts = [new(OrdersId, "desc", OrdersNodeId)],
            Parameters = [new(StatusParameter, "status", "string", "open")],
            Page = new() { Index = 2, Size = 10 },
            RowLimit = 100
        };

        var compiled = Compiler.Compile(request, OrdersModel(), new SqliteApplicationDataSourceProvider());

        Assert.Contains("SELECT", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@status", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10, compiled.PageSize);
        Assert.Single(compiled.Parameters);
    }

    [Fact]
    public void KeepsRawSqlOutsideTheStructuredContractAndCompilesControlledWritePlans()
    {
        var write = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            AccessMode = ApplicationQueryPlanAccessMode.ControlledWrite,
            WriteOperation = ApplicationQueryPlanWriteOperation.Update,
            WriteValues = new Dictionary<string, string> { [OrdersName] = NameParameter },
            Filters = [new(OrdersId, "eq", IdParameter, OrdersNodeId)],
            Parameters = [new(NameParameter, "name", "string", "new"), new(IdParameter, "id", "int", 1)]
        };

        Assert.DoesNotContain(typeof(ApplicationQueryPlanRequest).GetProperties(), property => property.Name.Equals("RawSql", StringComparison.OrdinalIgnoreCase));
        var compiled = Compiler.Compile(write, OrdersModel(), new SqliteApplicationDataSourceProvider());
        Assert.StartsWith("UPDATE", compiled.WriteSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("UPDATE", compiled.StatementKind);
    }

    [Fact]
    public void RejectsUnboundedControlledDeleteAndMissingExpectedWriteParameter()
    {
        var unbounded = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            AccessMode = ApplicationQueryPlanAccessMode.ControlledWrite,
            WriteOperation = ApplicationQueryPlanWriteOperation.Delete
        };
        var missingParameter = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            AccessMode = ApplicationQueryPlanAccessMode.ControlledWrite,
            WriteOperation = ApplicationQueryPlanWriteOperation.Insert,
            WriteValues = new Dictionary<string, string> { [OrdersName] = NameParameter }
        };

        Assert.Throws<ValidationException>(() => Compiler.Compile(unbounded, OrdersModel(), new SqliteApplicationDataSourceProvider()));
        Assert.Throws<ValidationException>(() => Compiler.Compile(missingParameter, OrdersModel(), new SqliteApplicationDataSourceProvider()));
    }

    [Fact]
    public void RejectsUndefinedParametersAndUnsupportedTypes()
    {
        var undefined = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId)],
            Filters = [new(OrdersStatus, "eq", "data:parameter:missing", OrdersNodeId)]
        };
        var unsupported = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId)],
            Filters = [new(OrdersStatus, "eq", StatusParameter, OrdersNodeId)],
            Parameters = [new(StatusParameter, "status", "sql", "SELECT 1")]
        };

        Assert.Throws<ValidationException>(() => Compiler.Compile(undefined, OrdersModel(), new SqliteApplicationDataSourceProvider()));
        Assert.Throws<ValidationException>(() => Compiler.Compile(unsupported, OrdersModel(), new SqliteApplicationDataSourceProvider()));
    }

    [Fact]
    public void RejectsInjectionInResourceAliasesAndSortDirections()
    {
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o; DROP TABLE users")],
            Columns = [new(OrdersId, "id; DROP", OrdersNodeId)],
            Sorts = [new(OrdersId, "desc; DROP", OrdersNodeId)]
        };

        Assert.Throws<ValidationException>(() => Compiler.Compile(request, OrdersModel(), new SqliteApplicationDataSourceProvider()));
    }

    [Fact]
    public void RejectsSchemaQualifiedObjectsWhenProviderDoesNotSupportSchemas()
    {
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId)]
        };

        var exception = Assert.Throws<ValidationException>(() => Compiler.Compile(request, OrdersModel("main"), new SqliteApplicationDataSourceProvider()));

        Assert.Contains("does not support schema", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("Sqlite", "\"orders\" AS \"o\"", "LIMIT 10 OFFSET 0")]
    [InlineData("MySql", "`orders` AS `o`", "LIMIT 10 OFFSET 0")]
    [InlineData("PostgreSQL", "\"orders\" AS \"o\"", "LIMIT 10 OFFSET 0")]
    [InlineData("SqlServer", "[orders] AS [o]", "OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY")]
    public void CompilesStructuredJoinAggregateGroupHavingWithProviderPagination(string providerName, string source, string pageSuffix)
    {
        IApplicationDataSourceProvider provider = providerName switch
        {
            "Sqlite" => new SqliteApplicationDataSourceProvider(),
            "MySql" => new MySqlApplicationDataSourceProvider(),
            "PostgreSQL" => new PostgreSqlApplicationDataSourceProvider(),
            _ => new SqlServerApplicationDataSourceProvider()
        };
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o"), new(CustomerNodeId, CustomersResourceId, "c")],
            Joins = [new("left", OrdersNodeId, OrdersId, CustomerNodeId, CustomerId)],
            Columns = [new(OrdersId, "orderId", OrdersNodeId), new(CustomerId, "customerCount", CustomerNodeId, "count")],
            Filters = [new(OrdersStatus, "eq", StatusParameter, OrdersNodeId)],
            GroupBy = [new(OrdersNodeId, OrdersStatus)],
            Having = [new(CustomerId, "gt", MinimumParameter, CustomerNodeId)],
            Sorts = [new(OrdersStatus, "desc", OrdersNodeId)],
            Parameters = [new(StatusParameter, "status", "string", "open"), new(MinimumParameter, "minimum", "int", 0)],
            Page = new() { Index = 1, Size = 10 },
            RowLimit = 10
        };

        var compiled = Compiler.Compile(request, OrdersAndCustomersModel(), provider);

        Assert.Contains(source, compiled.PageSql, StringComparison.Ordinal);
        Assert.Contains("JOIN", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COUNT(", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HAVING", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(pageSuffix, compiled.PageSql, StringComparison.Ordinal);
        Assert.Contains("@status", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@minimum", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsUnsupportedFunctionAndUnconnectedStructuredNode()
    {
        var unsupportedFunction = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersName, NodeId: OrdersNodeId, Function: "random")]
        };
        var unconnected = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o"), new(CustomerNodeId, CustomersResourceId, "c")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId)]
        };

        Assert.Throws<ValidationException>(() => Compiler.Compile(unsupportedFunction, OrdersModel(), new SqliteApplicationDataSourceProvider()));
        Assert.Throws<ValidationException>(() => Compiler.Compile(unconnected, OrdersAndCustomersModel(), new SqliteApplicationDataSourceProvider()));
    }

    [Fact]
    public void RejectsIdentifierInjectionAndConvertsTypedParameters()
    {
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: OrdersNodeId)],
            Filters = [new(OrdersAmount, "gte", "data:parameter:amount", OrdersNodeId)],
            Parameters = [new("data:parameter:amount", "amount", "decimal", "12.50")]
        };

        var compiled = Compiler.Compile(request, OrdersModel(), new SqliteApplicationDataSourceProvider());

        Assert.Equal(12.50m, compiled.Parameters.Single().Value);
        Assert.DoesNotContain("12.50", compiled.PageSql, StringComparison.Ordinal);
    }

    [Fact]
    public void HonorsCancellationBeforeCompilation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => Compiler.Compile(
            new ApplicationQueryPlanRequest { DataSourceId = "source-1", Nodes = [new(OrdersNodeId, OrdersResourceId, "o")], Columns = [new(OrdersId, NodeId: OrdersNodeId)] },
            OrdersModel(),
            new SqliteApplicationDataSourceProvider(),
            cancellation.Token));
    }

    [Fact]
    public void CompilesCanonicalMappingCacheParameterTypes()
    {
        var request = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersAmount, NodeId: OrdersNodeId)],
            Filters = [new(OrdersAmount, "gte", MinimumParameter, OrdersNodeId)],
            Parameters = [new(MinimumParameter, "minimum", ApplicationMappingCacheParameterType.Number, 12.5m)],
            Page = new() { Index = 1, Size = 10 },
            RowLimit = 10
        };

        var compiled = Compiler.Compile(request, OrdersModel(), new SqliteApplicationDataSourceProvider());

        Assert.Contains("WHERE", compiled.PageSql, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(12.5m, compiled.Parameters.Single().Value);
    }

    [Theory]
    [InlineData("Sqlite", "\"o\"", "\"c\"")]
    [InlineData("MySql", "`o`", "`c`")]
    [InlineData("PostgreSQL", "\"o\"", "\"c\"")]
    [InlineData("SqlServer", "[o]", "[c]")]
    public void CompilesCanonicalInnerJoinSqlGoldenForEveryProvider(string providerName, string leftAlias, string rightAlias)
    {
        var provider = Provider(providerName);
        var request = JoinRequest("inner");

        var compiled = Compiler.Compile(request, OrdersAndCustomersModel(), provider);

        var expected = providerName switch
        {
            "MySql" => "SELECT `o`.`id`, `c`.`id` FROM `orders` AS `o` INNER JOIN `customers` AS `c` ON `o`.`id` = `c`.`id` LIMIT 10 OFFSET 0",
            "SqlServer" => "SELECT [o].[id], [c].[id] FROM [orders] AS [o] INNER JOIN [customers] AS [c] ON [o].[id] = [c].[id] OFFSET 0 ROWS FETCH NEXT 10 ROWS ONLY",
            _ => $"SELECT {leftAlias}.\"id\", {rightAlias}.\"id\" FROM \"orders\" AS {leftAlias} INNER JOIN \"customers\" AS {rightAlias} ON {leftAlias}.\"id\" = {rightAlias}.\"id\" LIMIT 10 OFFSET 0"
        };
        Assert.Equal(expected, compiled.PageSql);
    }

    [Theory]
    [InlineData("Sqlite", false)]
    [InlineData("MySql", false)]
    [InlineData("PostgreSQL", true)]
    [InlineData("SqlServer", true)]
    public void EnforcesFullJoinCapabilityForEveryProvider(string providerName, bool supported)
    {
        var exception = Record.Exception(() => Compiler.Compile(JoinRequest("full"), OrdersAndCustomersModel(), Provider(providerName)));

        if (supported)
        {
            Assert.Null(exception);
            Assert.Contains("FULL JOIN", Compiler.Compile(JoinRequest("full"), OrdersAndCustomersModel(), Provider(providerName)).PageSql, StringComparison.Ordinal);
        }
        else
        {
            var validation = Assert.IsType<ValidationException>(exception);
            Assert.Contains("does not support FULL JOIN", validation.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void RejectsUnknownFieldNodeAndIllegalJoinEnum()
    {
        var unknownNode = new ApplicationQueryPlanRequest
        {
            DataSourceId = "source-1",
            Nodes = [new(OrdersNodeId, OrdersResourceId, "o")],
            Columns = [new(OrdersId, NodeId: "missing-node")]
        };
        var illegalJoin = JoinRequest("cross");

        Assert.Contains("unknown node", Assert.Throws<ValidationException>(() => Compiler.Compile(unknownNode, OrdersModel(), new SqliteApplicationDataSourceProvider())).Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Unsupported join type", Assert.Throws<ValidationException>(() => Compiler.Compile(illegalJoin, OrdersAndCustomersModel(), new SqliteApplicationDataSourceProvider())).Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ApplicationQueryPlanRequest JoinRequest(string type) => new()
    {
        DataSourceId = "source-1",
        Nodes = [new(OrdersNodeId, OrdersResourceId, "o"), new(CustomerNodeId, CustomersResourceId, "c")],
        Joins = [new(type, OrdersNodeId, OrdersId, CustomerNodeId, CustomerId)],
        Columns = [new(OrdersId, NodeId: OrdersNodeId), new(CustomerId, NodeId: CustomerNodeId)],
        Page = new() { Index = 1, Size = 10 },
        RowLimit = 10
    };

    private static IApplicationDataSourceProvider Provider(string providerName) => providerName switch
    {
        "Sqlite" => new SqliteApplicationDataSourceProvider(),
        "MySql" => new MySqlApplicationDataSourceProvider(),
        "PostgreSQL" => new PostgreSqlApplicationDataSourceProvider(),
        "SqlServer" => new SqlServerApplicationDataSourceProvider(),
        _ => throw new ArgumentOutOfRangeException(nameof(providerName))
    };

    private static ApplicationQueryPlanResolvedModel OrdersModel(string? schemaName = null) =>
        new([new(OrdersNodeId, "o", new(
            OrdersResourceId,
            ApplicationDataResourceKind.Table,
            schemaName,
            "orders",
            [
                new(OrdersId, "id", "int", false, OrdersId, "id"),
                new(OrdersName, "name", "string", false, OrdersName, "name"),
                new(OrdersStatus, "status", "string", true, OrdersStatus, "status"),
                new(OrdersAmount, "amount", "decimal", true, OrdersAmount, "amount")
            ],
            [],
            OrdersResourceId))]);

    private static ApplicationQueryPlanResolvedModel OrdersAndCustomersModel() =>
        new(
        [
            OrdersModel().Nodes[0],
            new(CustomerNodeId, "c", new(
                CustomersResourceId,
                ApplicationDataResourceKind.Table,
                null,
                "customers",
                [new(CustomerId, "id", "int", false, CustomerId, "id")],
                [],
                CustomersResourceId))
        ]);
}
