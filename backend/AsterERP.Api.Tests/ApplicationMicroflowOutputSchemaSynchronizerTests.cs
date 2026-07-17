using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowOutputSchemaSynchronizerTests
{
    [Fact]
    public void Synchronize_UsesReturnOutputSchemaAsPublicOutputContract()
    {
        var definition = new ApplicationMicroflowDefinition
        {
            Outputs =
            [
                new ApplicationMicroflowVariableDefinition
                {
                    Fields =
                    [
                        new ApplicationMicroflowFieldDefinition
                        {
                            DataType = "string",
                            FieldCode = "stale",
                            FieldName = "旧字段"
                        }
                    ],
                    ValueType = "array",
                    VariableCode = "orderListRows",
                    VariableName = "旧订单列表"
                }
            ],
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return-sql",
                    Name = "返回订单列表",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
                        {
                            SourceMode = "sqlScript",
                            ValueType = "array",
                            VariableCode = "orderListRows",
                            VariableName = "订单列表",
                            Fields =
                            [
                                Field("id", "ID", "string"),
                                Field("customerList", "客户列表", "array"),
                                Field("detailLines", "明细行", "array"),
                                Field("unitRows", "单位行", "array")
                            ]
                        }
                    }
                }
            ]
        };

        var synchronized = new ApplicationMicroflowOutputSchemaSynchronizer().Synchronize(definition);

        var output = Assert.Single(synchronized.Outputs);
        Assert.Equal("orderListRows", output.VariableCode);
        Assert.Equal("订单列表", output.VariableName);
        Assert.Equal("array", output.ValueType);
        Assert.Equal("return-sql", output.SourceNodeId);
        Assert.Equal(["id", "customerList", "detailLines", "unitRows"], output.Fields.Select(item => item.FieldCode).ToArray());
        Assert.Equal(["string", "array", "array", "array"], output.Fields.Select(item => item.DataType).ToArray());
        Assert.All(output.Fields, field => Assert.False(field.ReadOnly));
    }

    private static ApplicationMicroflowFieldDefinition Field(string code, string name, string dataType) =>
        new()
        {
            DataType = dataType,
            FieldCode = code,
            FieldName = name,
            Visible = true,
            Writable = false,
            Expression = new RuntimeValueExpressionDto
            {
                DataType = dataType,
                Kind = "ref",
                Ref = new RuntimeVariableRefDto
                {
                    DataType = dataType,
                    FieldPath = [code],
                    Label = $"SQL结果.{code}",
                    OutputKey = "sqlRow",
                    SourceType = "sqlResult",
                    VariableId = "sqlRow"
                }
            }
        };
}
