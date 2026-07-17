using AsterERP.Api.Application.Runtime;
using AsterERP.Contracts.Expressions;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared.Exceptions;
using Xunit;

namespace AsterERP.Api.Tests;

public sealed class RuntimeValueExpressionEvaluatorTests
{
    [Fact]
    public void Evaluate_LatestExpressionResolvesResourceAndLogic()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["form"] = new Dictionary<string, object?> { ["approved"] = true }
        });
        var expression = new ExpressionValueDto
        {
            Kind = "logic",
            DataType = "boolean",
            Operator = "and",
            Args =
            [
                new ExpressionValueDto { Kind = "literal", DataType = "boolean", Value = true },
                new ExpressionValueDto { Kind = "resourceRef", DataType = "boolean", ResourceId = "form:approved" }
            ]
        };

        Assert.Equal(true, evaluator.Evaluate(expression, context));
    }

    [Fact]
    public void ExpressionCanonicalizer_SortsObjectPropertiesAndCollectsStableDependencies()
    {
        var expression = new ExpressionValueDto
        {
            Kind = "object",
            Properties = new Dictionary<string, ExpressionValueDto>
            {
                ["z"] = new() { Kind = "resourceRef", DataType = "string", ResourceId = "form:z" },
                ["a"] = new() { Kind = "resourceRef", DataType = "string", ResourceId = "form:a" }
            }
        };

        var canonical = ExpressionValueCanonicalizer.Serialize(expression);

        Assert.True(canonical.IndexOf("\"a\"", StringComparison.Ordinal) < canonical.IndexOf("\"z\"", StringComparison.Ordinal));
        Assert.Equal(["form:a", "form:z"], ExpressionValueCanonicalizer.CollectDependencies(expression));
    }
    [Fact]
    public void Evaluate_ResolvesGlobalRefAndFunction()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["variables"] = new Dictionary<string, object?>
            {
                ["amount"] = "13",
                ["order"] = new Dictionary<string, object?>
                {
                    ["amount"] = "42"
                }
            }
        });

        var result = evaluator.Evaluate(new RuntimeValueExpressionDto
        {
            DataType = "number",
            FunctionId = "toNumber",
            Kind = "function",
            Args =
            [
                Ref("global", "order", ["amount"], "string")
            ]
        }, context);

        Assert.Equal(42m, result);
    }

    [Fact]
    public void Evaluate_ResolvesFlatGlobalRef()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["variables"] = new Dictionary<string, object?>
            {
                ["amount"] = "13"
            }
        });

        var result = evaluator.Evaluate(Ref("global", "amount", [], "string"), context);

        Assert.Equal("13", result);
    }

    [Fact]
    public void Evaluate_MigratesControlledLoopItemReferenceToCanonicalItemScope()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());

        var result = evaluator.Evaluate(
            Ref("loopItem", "item", [], "string"),
            new RuntimeExpressionEvaluationContext(new Dictionary<string, object?> { ["item"] = "current-item" }));

        Assert.Equal("current-item", result);
    }

    [Fact]
    public void Evaluate_BuildsObjectArrayAndTemplate()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["variables"] = new Dictionary<string, object?>
            {
                ["order"] = new Dictionary<string, object?> { ["orderNo"] = "SO-001" }
            }
        });

        var result = evaluator.Evaluate(new RuntimeValueExpressionDto
        {
            DataType = "object",
            Kind = "object",
            Properties =
            {
                ["orderNo"] = Ref("global", "order", ["orderNo"], "string"),
                ["tags"] = new RuntimeValueExpressionDto
                {
                    DataType = "array",
                    Kind = "array",
                    Items =
                    [
                        Literal("accepted", "string"),
                        new()
                        {
                            DataType = "string",
                            Kind = "template",
                            Items =
                            [
                                Literal("order:", "string"),
                                Ref("global", "order", ["orderNo"], "string")
                            ]
                        }
                    ]
                }
            }
        }, context);

        var mapped = Assert.IsAssignableFrom<IReadOnlyDictionary<string, object?>>(result);
        Assert.Equal("SO-001", mapped["orderNo"]);
        Assert.Equal(["accepted", "order:SO-001"], Assert.IsAssignableFrom<object[]>(mapped["tags"]));
    }

    [Fact]
    public void Evaluate_ReportsDescriptorWhenArrayDataTypeFails()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["variables"] = new Dictionary<string, object?> { ["items"] = "not-array" }
        });

        var exception = Assert.Throws<ValidationException>(() => evaluator.Evaluate(
            Ref("global", "items", [], "array"),
            context,
            new RuntimeExpressionEvaluationDescriptor
            {
                ExpressionName = "outputSchema.arrayExpression",
                OwnerId = "return",
                OwnerName = "Return Items",
                OwnerType = "MicroflowNode:return"
            }));

        Assert.Contains("ownerId=return", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ownerName=Return Items", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expressionName=outputSchema.arrayExpression", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Expression result must be an array", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_ReportsDescriptorWhenObjectDataTypeFails()
    {
        var evaluator = new RuntimeValueExpressionEvaluator(new RuntimeExpressionHelperCatalog());
        var context = new RuntimeExpressionEvaluationContext(new Dictionary<string, object?>
        {
            ["variables"] = new Dictionary<string, object?> { ["detail"] = 123 }
        });

        var exception = Assert.Throws<ValidationException>(() => evaluator.Evaluate(
            Ref("global", "detail", [], "object"),
            context,
            new RuntimeExpressionEvaluationDescriptor
            {
                ExpressionName = "bodyExpression",
                OwnerId = "api",
                OwnerName = "Call API",
                OwnerType = "MicroflowNode:callApi"
            }));

        Assert.Contains("ownerId=api", exception.Message, StringComparison.Ordinal);
        Assert.Contains("expressionName=bodyExpression", exception.Message, StringComparison.Ordinal);
        Assert.Contains("Expression result must be an object", exception.Message, StringComparison.Ordinal);
    }

    private static RuntimeValueExpressionDto Ref(
        string sourceType,
        string outputKey,
        List<string> fieldPath,
        string dataType) =>
        new()
        {
            DataType = dataType,
            Kind = "ref",
            Ref = new RuntimeVariableRefDto
            {
                DataType = dataType,
                FieldPath = fieldPath,
                Label = string.Join('.', new[] { outputKey }.Concat(fieldPath).Where(item => !string.IsNullOrWhiteSpace(item))),
                OutputKey = outputKey,
                SourceType = sourceType,
                VariableId = outputKey
            }
        };

    private static RuntimeValueExpressionDto Literal(object? value, string dataType) =>
        new()
        {
            DataType = dataType,
            Kind = "literal",
            Value = value
        };
}
