using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowPreviewResultBuilderTests
{
    [Fact]
    public void Build_CreatesDatasetsFromResultAndVariables()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var definition = Definition();
        var rows = new[]
        {
            new Dictionary<string, object?>
            {
                ["id"] = "order-1",
                ["amount"] = 12.5m,
                ["payload"] = new Dictionary<string, object?> { ["status"] = "new" }
            }
        };
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            rows,
            new Dictionary<string, object?>
            {
                ["items"] = rows,
                ["sqlRows"] = Array.Empty<Dictionary<string, object?>>(),
                ["keyword"] = "order"
            },
            ["start:start", "query:query-list", "return:return"]);

        var response = builder.Build("draft", definition, execution, 50, null);

        var result = Assert.Single(response.Datasets, item => item.Key == "result");
        Assert.Equal(1, result.TotalRows);
        Assert.False(result.Truncated);
        Assert.Contains(result.Fields, field => field.FieldCode == "id" && field.PrimaryKey);
        Assert.Equal("Number", result.Fields.Single(field => field.FieldCode == "amount").DataType);
        var payload = Assert.IsType<Dictionary<string, object?>>(result.Rows.Single()["payload"]);
        Assert.Equal("new", payload["status"]);
        Assert.Contains(response.Datasets, item => item.Key == "variables.items");
        Assert.Contains(response.Datasets, item => item.Key == "variables.sqlRows");
        Assert.Equal("result", response.PrimaryDatasetKey);
        Assert.Contains(response.Variables, item => item.Name == "items" && item.DatasetKey == "variables.items");
        Assert.Equal(3, response.Trace.Count);
        Assert.Equal("查询订单", response.Trace[1].NodeName);
    }

    [Fact]
    public void Build_TruncatesRowsByMaxRows()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var rows = Enumerable.Range(1, 3)
            .Select(index => new Dictionary<string, object?> { ["id"] = $"row-{index}" })
            .ToArray();
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            rows,
            [],
            []);

        var response = builder.Build("published", Definition(), execution, 2, null);

        var result = Assert.Single(response.Datasets);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(2, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public void Build_UsesPreferredResultPathWhenItIsDataset()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var rows = new[]
        {
            new Dictionary<string, object?> { ["id"] = "row-1" }
        };
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            null,
            new Dictionary<string, object?>
            {
                ["customRows"] = rows
            },
            []);

        var response = builder.Build("draft", Definition(), execution, 20, "variables.customRows");

        Assert.Equal("preferred", response.PrimaryDatasetKey);
        var preferred = Assert.Single(response.Datasets, item => item.Key == "preferred");
        Assert.Equal("variables.customRows", preferred.SourcePath);
        Assert.Equal(1, preferred.TotalRows);
    }

    [Fact]
    public void Build_UsesConfiguredOutputSchemaForEmptyResultRows()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var definition = Definition();
        definition.Outputs.Add(new ApplicationMicroflowVariableDefinition
        {
            VariableCode = "orderListRows",
            VariableName = "订单列表",
            ValueType = "array",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "orderNo",
                    FieldName = "订单号",
                    Visible = true,
                    Writable = false
                },
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "number",
                    FieldCode = "amount",
                    FieldName = "金额",
                    Visible = true,
                    Writable = false
                }
            ]
        });
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            Array.Empty<Dictionary<string, object?>>(),
            new Dictionary<string, object?>
            {
                ["orderListRows"] = Array.Empty<Dictionary<string, object?>>()
            },
            []);

        var response = builder.Build("draft", definition, execution, 50, null);

        var result = Assert.Single(response.Datasets, item => item.Key == "result");
        Assert.Empty(result.Rows);
        Assert.Contains(result.Fields, field => field.FieldCode == "orderNo" && field.FieldName == "订单号");
        Assert.Contains(result.Fields, field => field.FieldCode == "amount" && field.DataType == "number");

        var outputDataset = Assert.Single(response.Datasets, item => item.Key == "variables.orderListRows");
        Assert.Empty(outputDataset.Rows);
        Assert.Equal(["orderNo", "amount"], outputDataset.Fields.Select(field => field.FieldCode).ToArray());
        Assert.Contains(response.Variables, item => item.Name == "orderListRows" && item.DatasetKey == "variables.orderListRows");
    }

    [Fact]
    public void Build_UsesReturnNodeOutputSchemaForEmptyResultRows()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var definition = Definition();
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new Dictionary<string, object?>
        {
            ["variableCode"] = "orderListRows",
            ["variableName"] = "订单列表",
            ["valueType"] = "array",
            ["fields"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["dataType"] = "string",
                    ["fieldCode"] = "orderNo",
                    ["fieldName"] = "订单号",
                    ["visible"] = true,
                    ["writable"] = false
                },
                new Dictionary<string, object?>
                {
                    ["dataType"] = "number",
                    ["fieldCode"] = "amount",
                    ["fieldName"] = "金额",
                    ["visible"] = true,
                    ["writable"] = false
                }
            }
        };
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            Array.Empty<Dictionary<string, object?>>(),
            new Dictionary<string, object?>
            {
                ["orderListRows"] = Array.Empty<Dictionary<string, object?>>()
            },
            []);

        var response = builder.Build("draft", definition, execution, 50, "variables.orderListRows");

        var preferred = Assert.Single(response.Datasets, item => item.Key == "preferred");
        Assert.Empty(preferred.Rows);
        Assert.Equal(["orderNo", "amount"], preferred.Fields.Select(field => field.FieldCode).ToArray());
        Assert.Equal("preferred", response.PrimaryDatasetKey);

        var outputDataset = Assert.Single(response.Datasets, item => item.Key == "variables.orderListRows");
        Assert.Equal(["订单号", "金额"], outputDataset.Fields.Select(field => field.FieldName).ToArray());
    }

    [Fact]
    public void Build_UsesProducerNodeOutputSchemaForEmptyVariableRows()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var definition = Definition();
        var queryNode = definition.Nodes.Single(node => node.Type == "query");
        queryNode.Config["outputSchema"] = new Dictionary<string, object?>
        {
            ["variableCode"] = "orderListRows",
            ["variableName"] = "订单列表",
            ["valueType"] = "array",
            ["fields"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["dataType"] = "string",
                    ["fieldCode"] = "orderNo",
                    ["fieldName"] = "订单号",
                    ["visible"] = true,
                    ["writable"] = false
                },
                new Dictionary<string, object?>
                {
                    ["dataType"] = "number",
                    ["fieldCode"] = "amount",
                    ["fieldName"] = "金额",
                    ["visible"] = true,
                    ["writable"] = false
                }
            }
        };
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            Array.Empty<Dictionary<string, object?>>(),
            new Dictionary<string, object?>
            {
                ["orderListRows"] = Array.Empty<Dictionary<string, object?>>()
            },
            []);

        var response = builder.Build("draft", definition, execution, 50, "variables.orderListRows");

        var preferred = Assert.Single(response.Datasets, item => item.Key == "preferred");
        Assert.Empty(preferred.Rows);
        Assert.Equal(["orderNo", "amount"], preferred.Fields.Select(field => field.FieldCode).ToArray());

        var variableDataset = Assert.Single(response.Datasets, item => item.Key == "variables.orderListRows");
        Assert.Equal(["订单号", "金额"], variableDataset.Fields.Select(field => field.FieldName).ToArray());
    }

    [Fact]
    public void Build_PrefersReturnNodeOutputSchemaOverProducerNodeSchema()
    {
        var builder = new ApplicationMicroflowPreviewResultBuilder();
        var definition = Definition();
        var queryNode = definition.Nodes.Single(node => node.Type == "query");
        queryNode.Config["outputSchema"] = new Dictionary<string, object?>
        {
            ["variableCode"] = "orderListRows",
            ["variableName"] = "订单列表",
            ["valueType"] = "array",
            ["fields"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["dataType"] = "string",
                    ["fieldCode"] = "orderNo",
                    ["fieldName"] = "订单号",
                    ["visible"] = true,
                    ["writable"] = false
                },
                new Dictionary<string, object?>
                {
                    ["dataType"] = "number",
                    ["fieldCode"] = "amount",
                    ["fieldName"] = "金额",
                    ["visible"] = true,
                    ["writable"] = false
                }
            }
        };
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new Dictionary<string, object?>
        {
            ["variableCode"] = "orderListRows",
            ["variableName"] = "返回订单",
            ["valueType"] = "array",
            ["fields"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["dataType"] = "string",
                    ["fieldCode"] = "orderNo",
                    ["fieldName"] = "订单号",
                    ["visible"] = true,
                    ["writable"] = false
                }
            }
        };
        var execution = new ApplicationMicroflowExecuteResponse(
            "order_query",
            Array.Empty<Dictionary<string, object?>>(),
            new Dictionary<string, object?>
            {
                ["orderListRows"] = Array.Empty<Dictionary<string, object?>>()
            },
            []);

        var response = builder.Build("draft", definition, execution, 50, "variables.orderListRows");

        var preferred = Assert.Single(response.Datasets, item => item.Key == "preferred");
        Assert.Equal(["orderNo"], preferred.Fields.Select(field => field.FieldCode).ToArray());
        var outputDataset = Assert.Single(response.Datasets, item => item.Key == "variables.orderListRows");
        Assert.Equal("返回订单", outputDataset.Title);
        Assert.Equal(["orderNo"], outputDataset.Fields.Select(field => field.FieldCode).ToArray());
    }

    private static ApplicationMicroflowDefinition Definition() =>
        new()
        {
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "开始",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "query-list",
                    Name = "查询订单",
                    Type = "query"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "返回",
                    Type = "return"
                }
            ]
        };
}
