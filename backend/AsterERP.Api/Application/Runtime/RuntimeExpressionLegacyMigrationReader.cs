using AsterERP.Contracts.Expressions;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

/// <summary>
/// One-way boundary reader for persisted pre-ExpressionValue runtime payloads.
/// It is intentionally not a runtime evaluator: callers receive a canonical
/// AST which is then validated and evaluated by the normal ExpressionValue path.
/// </summary>
public static class RuntimeExpressionLegacyMigrationReader
{
    public static ExpressionValueDto Read(RuntimeValueExpressionDto expression) => Convert(expression);

    private static ExpressionValueDto Convert(RuntimeValueExpressionDto expression)
    {
        var kind = expression.Kind?.Trim() ?? string.Empty;
        return kind switch
        {
            "literal" => Copy(expression, "literal"),
            "resourceRef" or "functionCall" or "conversion" or "condition" or "logic" or "object" or "array" or "template" or "defaultValue" => Copy(expression, kind),
            "ref" => new ExpressionValueDto
            {
                Version = "latest",
                Kind = "resourceRef",
                DataType = expression.DataType,
                ResourceId = ToResourceId(expression.Ref)
            },
            "function" => Copy(expression, "functionCall"),
            _ => throw new ValidationException($"Legacy runtime expression kind '{expression.Kind}' is MigrationBlocked.", ErrorCodes.ParameterInvalid)
        };
    }

    private static ExpressionValueDto Copy(RuntimeValueExpressionDto expression, string kind) => new()
    {
        Version = "latest",
        Kind = kind,
        DataType = expression.DataType,
        Value = expression.Value,
        ResourceId = expression.ResourceId,
        FunctionId = expression.FunctionId,
        Args = expression.Args.Select(Convert).ToList(),
        Input = expression.Input is null ? null : Convert(expression.Input),
        When = expression.When is null ? null : Convert(expression.When),
        Then = expression.Then is null ? null : Convert(expression.Then),
        Otherwise = expression.Otherwise is null ? null : Convert(expression.Otherwise),
        Operator = expression.Operator,
        Properties = expression.Properties.ToDictionary(item => item.Key, item => Convert(item.Value), StringComparer.Ordinal),
        Items = expression.Items.Select(Convert).ToList(),
        Pipeline = expression.Pipeline,
        Fallback = expression.Fallback,
        Dependencies = [],
        CanonicalHash = null
    };

    private static string ToResourceId(RuntimeVariableRefDto? reference)
    {
        if (reference is null) throw new ValidationException("Legacy runtime ref is MigrationBlocked because ref is missing.", ErrorCodes.ParameterInvalid);
        var scope = NormalizeScope(reference.SourceType);
        if (scope == "item" && string.Equals(reference.SourceType, "loopItem", StringComparison.Ordinal) &&
            string.Equals(reference.OutputKey, "item", StringComparison.Ordinal) && reference.FieldPath.Count == 0)
        {
            return "item:*";
        }
        var segments = new[] { reference.OutputKey }.Concat(reference.FieldPath)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .ToArray();
        if (segments.Length == 0) throw new ValidationException("Legacy runtime ref is MigrationBlocked because it has no stable resource path.", ErrorCodes.ParameterInvalid);
        return $"{scope}:{string.Join('.', segments)}";
    }

    private static string NormalizeScope(string? sourceType) => sourceType?.Trim() switch
    {
        "global" or "nodeOutput" or "nodeInput" or "trigger" => "variables",
        "loopItem" => "item",
        "api" or "component" or "currentRow" or "form" or "microflow" or "model" or "page" or "system" or "tableRow" or "variables" or "workflow" or "item" or "lineItem" or "sqlResult" => sourceType.Trim(),
        _ => throw new ValidationException($"Legacy runtime ref source '{sourceType}' is MigrationBlocked.", ErrorCodes.ParameterInvalid)
    };
}
