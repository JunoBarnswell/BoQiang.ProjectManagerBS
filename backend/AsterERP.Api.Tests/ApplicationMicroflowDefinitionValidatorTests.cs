using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Contracts.Runtime;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class ApplicationMicroflowDefinitionValidatorTests
{
    [Fact]
    public void Validate_RejectsNodesWithoutRuntimeImplementation()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("startWorkflow");

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("不支持的微流节点类型: startWorkflow", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsRemovedRunSqlNode()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("runSql");

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("runSql 节点已移除", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsCompositeRuntimeNodes()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("compositeUpdate");

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsDetailRuntimeNode()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("detail");

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsCompositeDetailRuntimeNode()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("compositeDetail");

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsQueryRuntimeNodeAlias()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("query");

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsVariableSchemaBoundToDomainObject()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("query");
        definition.DomainObjects.Add(new ApplicationMicroflowDomainObjectDefinition
        {
            ObjectCode = "order",
            ObjectName = "订单",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "orderNo",
                    FieldName = "订单号",
                    Required = true
                },
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "decimal",
                    FieldCode = "amount",
                    FieldName = "金额"
                }
            ]
        });
        definition.Inputs.Add(new ApplicationMicroflowVariableDefinition
        {
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "orderNo",
                    FieldName = "订单号",
                    Required = true
                }
            ],
            SchemaObjectCode = "order",
            ValueType = "object",
            VariableCode = "form",
            VariableName = "订单表头"
        });

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsInvalidVariableSchema()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("query");
        definition.Inputs.Add(new ApplicationMicroflowVariableDefinition
        {
            SchemaObjectCode = "missing_order",
            ValueType = "array",
            VariableCode = "detailLines",
            VariableName = "明细",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition { FieldCode = "productId", FieldName = "商品" },
                new ApplicationMicroflowFieldDefinition { FieldCode = "productId", FieldName = "商品重复" },
                new ApplicationMicroflowFieldDefinition { FieldName = "空编码" }
            ]
        });
        definition.Inputs.Add(new ApplicationMicroflowVariableDefinition
        {
            ValueType = "array",
            VariableCode = "detailLines",
            VariableName = "重复明细"
        });
        definition.Variables.Add(new ApplicationMicroflowVariableDefinition
        {
            ValueType = "string",
            VariableName = "空编码变量"
        });

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("输入变量编码重复: detailLines", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("输入变量 detailLines 绑定的领域对象不存在: missing_order", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("输入变量 detailLines 字段编码重复: productId", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("输入变量 detailLines 字段编码不能为空", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("过程变量编码不能为空", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsReturnWithoutOutputSchemaFields()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("query");
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields = []
        };

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("Return 节点 Return(return) 未配置返回字段", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsInvalidReturnFieldDefinitions()
    {
        var validator = new ApplicationMicroflowDefinitionValidator();
        var definition = CreateDefinition("query");
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "orderCount",
                    FieldName = "订单数",
                    DataType = "number",
                    Expression = Expression("variables", "orders", "array")
                },
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "orderCount",
                    FieldName = "重复订单数",
                    DataType = "number",
                    Expression = Expression("variables", "orders", "array")
                },
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "missingSource",
                    FieldName = "缺少来源",
                    DataType = "string"
                },
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "emptySourcePath",
                    FieldName = "空来源路径",
                    DataType = "string",
                    Expression = new RuntimeValueExpressionDto
                    {
                        DataType = "string",
                        Kind = "ref"
                    }
                }
            ]
        };

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("Return 节点 Return(return) 返回字段编码重复: orderCount", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Return 节点 Return(return) 字段 missingSource 缺少来源表达式", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("Return 节点 Return(return) 字段 emptySourcePath 缺少来源表达式", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsGlobalVariableNodeWithoutEdges()
    {
        var validator = CreateStrictValidator();
        var definition = CreateDefinition("query");
        definition.Variables.Add(new ApplicationMicroflowVariableDefinition
        {
            VariableCode = "value",
            VariableName = "返回值",
            ValueType = "string"
        });
        definition.Nodes.Add(new ApplicationMicroflowNodeDefinition
        {
            Id = "globals",
            Name = "全局变量",
            Type = "globalVariables",
            Config = new Dictionary<string, object?>
            {
                ["variables"] = new List<ApplicationMicroflowVariableDefinition>
                {
                    new()
                    {
                        DefaultValue = 10,
                        ValueType = "number",
                        VariableCode = "threshold",
                        VariableName = "阈值"
                    }
                }
            }
        });

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_RejectsEdgesConnectedToGlobalVariableNode()
    {
        var validator = CreateStrictValidator();
        var definition = CreateDefinition("query");
        definition.Variables.Add(new ApplicationMicroflowVariableDefinition
        {
            VariableCode = "value",
            VariableName = "返回值",
            ValueType = "string"
        });
        definition.Nodes.Add(new ApplicationMicroflowNodeDefinition
        {
            Id = "globals",
            Name = "全局变量",
            Type = "globalVariables"
        });
        definition.Edges.Add(new ApplicationMicroflowEdgeDefinition
        {
            Id = "edge-start-globals",
            SourceNodeId = "start",
            TargetNodeId = "globals"
        });

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("全局变量节点不能作为连线目标: 全局变量(globals)", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateGlobalVariablesAndInvalidExpressionReferences()
    {
        var validator = CreateStrictValidator();
        var definition = CreateDefinition("query");
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "missing",
                    FieldName = "缺失变量",
                    Expression = FunctionExpression("notExists", "string", Expression("variables", "missing.code", "string"))
                }
            ]
        };
        definition.Nodes.Add(new ApplicationMicroflowNodeDefinition
        {
            Id = "globalsA",
            Name = "变量 A",
            Type = "globalVariables",
            Config = new Dictionary<string, object?>
            {
                ["variables"] = new List<ApplicationMicroflowVariableDefinition>
                {
                    new() { VariableCode = "dup", VariableName = "重复", ValueType = "string" }
                }
            }
        });
        definition.Nodes.Add(new ApplicationMicroflowNodeDefinition
        {
            Id = "globalsB",
            Name = "变量 B",
            Type = "globalVariables",
            Config = new Dictionary<string, object?>
            {
                ["variables"] = new List<ApplicationMicroflowVariableDefinition>
                {
                    new() { VariableCode = "dup", VariableName = "重复", ValueType = "string" }
                }
            }
        });

        var errors = validator.Validate(definition);

        Assert.Contains(errors, error => error.Contains("全局变量编码重复: dup", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("使用了不支持的函数: notExists", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains("引用变量不存在: missing.code", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AllowsNestedObjectFieldPathsForSetVariableAndReturn()
    {
        var validator = CreateStrictValidator();
        var definition = CreateDefinition("setVariable");
        var setVariableNode = definition.Nodes.Single(node => node.Id == "middle");
        setVariableNode.Name = "记录最后循环项";
        setVariableNode.Config["variableCode"] = "acceptance.loopLastItem";
        setVariableNode.Config["valueExpression"] = Expression("item", "", "object");
        definition.Variables.Add(new ApplicationMicroflowVariableDefinition
        {
            VariableCode = "acceptance",
            VariableName = "验收上下文",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "object",
                    FieldCode = "loopLastItem",
                    FieldName = "最后循环项",
                    Writable = true
                },
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "loopLastItem.product_name",
                    FieldName = "最后循环产品",
                    Writable = true
                }
            ]
        });
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "lastProduct",
                    FieldName = "最后循环产品",
                    Expression = Expression("variables", "acceptance.loopLastItem.product_name", "string")
                }
            ]
        };

        var errors = validator.Validate(definition);

        Assert.DoesNotContain(errors, error => error.Contains("目标变量不存在", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("引用变量不存在", StringComparison.Ordinal));
        Assert.DoesNotContain(errors, error => error.Contains("引用字段不存在", StringComparison.Ordinal));
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_AcceptsLoopItemAliasAndCommonAggregateHelpers()
    {
        var validator = CreateStrictValidator();
        var definition = CreateDefinition("loop");
        var loopNode = definition.Nodes.Single(node => node.Id == "middle");
        loopNode.Name = "循环订单行";
        loopNode.Config["collectionExpression"] = Expression("variables", "items", "array");
        loopNode.Config["itemVariable"] = "lineItem";
        definition.Variables.Add(new ApplicationMicroflowVariableDefinition
        {
            VariableCode = "items",
            VariableName = "订单行",
            ValueType = "array",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "product_name",
                    FieldName = "产品"
                },
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "number",
                    FieldCode = "amount",
                    FieldName = "金额"
                }
            ]
        });
        var returnNode = definition.Nodes.Single(node => node.Type == "return");
        returnNode.Config["outputSchema"] = new ApplicationMicroflowOutputSchemaDefinition
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "string",
                    FieldCode = "lastProduct",
                    FieldName = "最后产品",
                    Expression = Expression("lineItem", "product_name", "string")
                },
                new ApplicationMicroflowFieldDefinition
                {
                    DataType = "number",
                    FieldCode = "amountTotal",
                    FieldName = "金额合计",
                    Expression = FunctionExpression(
                        "add",
                        "number",
                        FunctionExpression("sum", "number", Expression("variables", "items", "array"), LiteralExpression("amount", "string")),
                        LiteralExpression(1, "number"))
                }
            ]
        };

        var errors = validator.Validate(definition);

        Assert.Empty(errors);
    }

    private static ApplicationMicroflowDefinition CreateDefinition(string middleNodeType) =>
        new()
        {
            SchemaVersion = 1,
            Nodes =
            [
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "start",
                    Name = "Start",
                    Type = "start"
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "middle",
                    Name = "Middle",
                    Type = middleNodeType
                },
                new ApplicationMicroflowNodeDefinition
                {
                    Id = "return",
                    Name = "Return",
                    Type = "return",
                    Config = new Dictionary<string, object?>
                    {
                        ["outputSchema"] = ReturnOutputSchema()
                    }
                }
            ],
            Edges =
            [
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-start-middle",
                    SourceNodeId = "start",
                    TargetNodeId = "middle"
                },
                new ApplicationMicroflowEdgeDefinition
                {
                    Id = "edge-middle-return",
                    SourceNodeId = "middle",
                    TargetNodeId = "return"
                }
            ]
        };

    private static ApplicationMicroflowOutputSchemaDefinition ReturnOutputSchema() =>
        new()
        {
            VariableCode = "result",
            VariableName = "返回结果",
            ValueType = "object",
            Fields =
            [
                new ApplicationMicroflowFieldDefinition
                {
                    FieldCode = "value",
                    FieldName = "返回值",
                    DataType = "string",
                    Expression = Expression("variables", "value", "string")
                }
            ]
        };

    private static RuntimeValueExpressionDto Expression(string source, string path, string dataType) =>
        ReferenceExpression(source, path, dataType);

    private static RuntimeValueExpressionDto FunctionExpression(
        string functionId,
        string dataType,
        params RuntimeValueExpressionDto[] args) =>
        new()
        {
            Args = args.ToList(),
            DataType = dataType,
            FunctionId = functionId,
            Kind = "function"
        };

    private static RuntimeValueExpressionDto LiteralExpression(object? value, string dataType) =>
        new()
        {
            DataType = dataType,
            Kind = "literal",
            Value = value
        };

    private static RuntimeValueExpressionDto ReferenceExpression(
        string source,
        string path,
        string dataType)
    {
        var parts = path
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var sourceType = source.Equals("variables", StringComparison.OrdinalIgnoreCase)
            ? "global"
            : source.Equals("inputs", StringComparison.OrdinalIgnoreCase)
                ? "trigger"
                : source.Trim();
        var outputKey = parts.FirstOrDefault() ?? string.Empty;
        var fieldPath = parts.Skip(1).ToList();
        if (!source.Equals("variables", StringComparison.OrdinalIgnoreCase) &&
            !source.Equals("inputs", StringComparison.OrdinalIgnoreCase) &&
            (source.Equals("item", StringComparison.OrdinalIgnoreCase) ||
             source.Equals("currentRow", StringComparison.OrdinalIgnoreCase) ||
             source.Equals("lineItem", StringComparison.OrdinalIgnoreCase)))
        {
            fieldPath = parts;
            outputKey = string.Empty;
        }

        return new RuntimeValueExpressionDto
        {
            DataType = dataType,
            Kind = "ref",
            Ref = new RuntimeVariableRefDto
            {
                DataType = dataType,
                FieldPath = fieldPath,
                Label = string.IsNullOrWhiteSpace(outputKey)
                    ? string.Join('.', fieldPath)
                    : string.Join('.', new[] { outputKey }.Concat(fieldPath)),
                OutputKey = outputKey,
                SourceType = sourceType,
                VariableId = string.IsNullOrWhiteSpace(outputKey) ? source : outputKey
            }
        };
    }

    private static ApplicationMicroflowDefinitionValidator CreateStrictValidator() =>
        new(new ApplicationMicroflowExpressionReferenceValidator(new RuntimeExpressionHelperCatalog()));
}
