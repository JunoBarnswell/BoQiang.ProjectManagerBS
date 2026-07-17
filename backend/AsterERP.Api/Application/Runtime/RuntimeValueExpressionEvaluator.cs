using System.Text.Json;
using AsterERP.Contracts.Expressions;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Runtime;

/// <summary>Evaluates the canonical ExpressionValue AST. Legacy DTOs are converted at a migration boundary.</summary>
public sealed class RuntimeValueExpressionEvaluator(RuntimeExpressionHelperCatalog functionCatalog)
{
    public object? Evaluate(RuntimeValueExpressionDto expression, RuntimeExpressionEvaluationContext context) =>
        EvaluateLatest(RuntimeExpressionLegacyMigrationReader.Read(expression), context);

    public object? Evaluate(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context) =>
        EvaluateLatest(expression, context);

    public object? Evaluate(
        RuntimeValueExpressionDto expression,
        RuntimeExpressionEvaluationContext context,
        RuntimeExpressionEvaluationDescriptor? descriptor)
    {
        try
        {
            return EvaluateLatest(RuntimeExpressionLegacyMigrationReader.Read(expression), context);
        }
        catch (ValidationException exception) when (descriptor is not null)
        {
            throw new ValidationException($"{exception.Message}; {BuildDescriptorContext(expression, context, descriptor)}", ErrorCodes.ParameterInvalid);
        }
    }

    public object? ApplyHelpers(object? value, IReadOnlyList<RuntimeExpressionHelperDto> helpers)
    {
        var result = value;
        foreach (var helper in helpers) result = functionCatalog.Apply(result, helper);
        return result;
    }

    private object? EvaluateLatest(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context)
    {
        ExpressionValueContractValidator.Validate(expression);
        return ValidateLatestDataType(NormalizeJsonValue(EvaluateLatestCore(expression, context)), expression.DataType);
    }

    private object? EvaluateLatestCore(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context) => expression.Kind switch
    {
        "literal" => expression.Value,
        "resourceRef" => ResolveLatestResource(expression.ResourceId, context),
        "functionCall" => EvaluateLatestFunction(expression, context),
        "conversion" => ApplyLatestConversions(expression, EvaluateLatestRequired(expression.Input, context)),
        "condition" => Convert.ToBoolean(EvaluateLatestRequired(expression.When, context), global::System.Globalization.CultureInfo.InvariantCulture)
            ? EvaluateLatestRequired(expression.Then, context)
            : EvaluateLatestRequired(expression.Otherwise, context),
        "logic" => EvaluateLatestLogic(expression, context),
        "object" => expression.Properties.ToDictionary(item => item.Key, item => EvaluateLatest(item.Value, context), StringComparer.OrdinalIgnoreCase),
        "array" => expression.Items.Select(item => EvaluateLatest(item, context)).ToArray(),
        "template" => string.Concat(expression.Items.Select(item => EvaluateLatest(item, context)?.ToString() ?? string.Empty)),
        "defaultValue" => EvaluateLatestDefault(expression, context),
        _ => throw new ValidationException($"Expression node kind is not supported: {expression.Kind}.", ErrorCodes.ParameterInvalid)
    };

    private object? EvaluateLatestFunction(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context)
    {
        var values = expression.Args.Select(item => EvaluateLatest(item, context)).ToArray();
        return functionCatalog.Apply(values.Length > 0 ? values[0] : null, new RuntimeExpressionHelperDto
        {
            Name = expression.FunctionId ?? string.Empty,
            Args = BuildFunctionArgs(values)
        });
    }

    private object? EvaluateLatestLogic(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context)
    {
        var values = expression.Args.Select(item => EvaluateLatestBoolean(item, context)).ToArray();
        return expression.Operator switch
        {
            "and" => values.All(item => item),
            "or" => values.Any(item => item),
            "not" => !values[0],
            _ => throw new ValidationException($"Expression logic operator is not supported: {expression.Operator}.", ErrorCodes.ParameterInvalid)
        };
    }

    private bool EvaluateLatestBoolean(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context) =>
        EvaluateLatest(expression, context) is bool value && value;

    private object? EvaluateLatestDefault(ExpressionValueDto expression, RuntimeExpressionEvaluationContext context)
    {
        var value = EvaluateLatestRequired(expression.Input, context);
        return value is null ? expression.Fallback : value;
    }

    private object? EvaluateLatestRequired(ExpressionValueDto? expression, RuntimeExpressionEvaluationContext context) =>
        expression is null
            ? throw new ValidationException("Expression child node is required.", ErrorCodes.ParameterInvalid)
            : EvaluateLatest(expression, context);

    private static object? ResolveLatestResource(string? resourceId, RuntimeExpressionEvaluationContext context)
    {
        var normalized = resourceId?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ValidationException("Expression resourceId is required.", ErrorCodes.ParameterInvalid);
        if (context.Sources.TryGetValue(normalized, out var direct)) return direct;

        var separator = normalized.IndexOf(':');
        if (separator <= 0 || separator == normalized.Length - 1)
        {
            throw new ValidationException($"Expression resource is not registered: {normalized}.", ErrorCodes.ParameterInvalid);
        }

        var sourceName = normalized[..separator];
        var path = normalized[(separator + 1)..];
        if (!context.Sources.TryGetValue(sourceName, out var source))
        {
            throw new ValidationException($"Expression resource source is not registered: {sourceName}.", ErrorCodes.ParameterInvalid);
        }

        if (path == "*") return source;
        return RuntimeExpressionPathReader.Read(source, path)
            ?? throw new ValidationException($"Expression resource is not registered: {normalized}.", ErrorCodes.ParameterInvalid);
    }

    private static object? ApplyLatestConversions(ExpressionValueDto expression, object? value)
    {
        foreach (var step in expression.Pipeline)
        {
            value = step.Name switch
            {
                "numberToString" when value is decimal or double or float or int or long => value.ToString(),
                "booleanToString" when value is bool boolean => boolean.ToString().ToLowerInvariant(),
                "stringToNumber" when value is string text && decimal.TryParse(text, out var number) => number,
                "stringToBoolean" when value is string booleanText && bool.TryParse(booleanText, out var booleanValue) => booleanValue,
                "arrayToJson" when value is global::System.Collections.IEnumerable => JsonSerializer.Serialize(value),
                "objectToJson" when value is not null && value.GetType().IsClass && value is not global::System.Collections.IEnumerable => JsonSerializer.Serialize(value),
                _ => throw new ValidationException($"Expression conversion failed: {step.Name}.", ErrorCodes.ParameterInvalid)
            };
        }

        return value;
    }

    private static object? ValidateLatestDataType(object? value, string dataType)
    {
        if (value is null) return null;
        return dataType.Trim().ToLowerInvariant() switch
        {
            "array" when value is string or not global::System.Collections.IEnumerable => throw new ValidationException("Expression result must be an array.", ErrorCodes.ParameterInvalid),
            "object" when value.GetType().IsPrimitive => throw new ValidationException("Expression result must be an object.", ErrorCodes.ParameterInvalid),
            _ => value
        };
    }

    private static Dictionary<string, object?> BuildFunctionArgs(IReadOnlyList<object?> values)
    {
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (values.Count <= 1) return args;

        args["value"] = values[1];
        args["field"] = values[1];
        args["path"] = values[1];
        args["separator"] = values[1];
        args["count"] = values[1];
        args["length"] = values[1];
        args["digits"] = values[1];
        args["min"] = values[1];
        args["start"] = values[1];
        args["oldValue"] = values[1];
        args["from"] = values[1];
        args["search"] = values[1];
        args["currency"] = values[1];
        args["symbol"] = values[1];
        args["trueText"] = values[1];
        args["whenTrue"] = values[1];
        if (values.Count > 2)
        {
            args["valueTo"] = values[2];
            args["max"] = values[2];
            args["end"] = values[2];
            args["newValue"] = values[2];
            args["to"] = values[2];
            args["replace"] = values[2];
            args["falseText"] = values[2];
            args["whenFalse"] = values[2];
        }
        if (values.Count > 3) args["mask"] = values[3];
        for (var index = 1; index < values.Count; index++) args[$"arg{index}"] = values[index];
        return args;
    }

    private static string BuildDescriptorContext(
        RuntimeValueExpressionDto expression,
        RuntimeExpressionEvaluationContext context,
        RuntimeExpressionEvaluationDescriptor descriptor)
    {
        var actualValue = TryReadLegacyActualValue(expression, context);
        var reference = expression.Ref;
        var path = reference is null || reference.FieldPath.Count == 0 ? "(root)" : string.Join('.', reference.FieldPath);
        var parts = new List<string>
        {
            $"ownerType={descriptor.OwnerType}",
            $"ownerId={descriptor.OwnerId}",
            $"ownerName={descriptor.OwnerName}",
            $"expressionName={descriptor.ExpressionName}",
            $"modelCode={descriptor.ModelCode}",
            $"bindingKey={descriptor.BindingKey}",
            $"kind={expression.Kind}",
            $"dataType={expression.DataType}",
            $"ref={reference?.SourceType}:{reference?.OutputKey}:{path}",
            $"actualType={actualValue?.GetType().Name ?? "null"}"
        };
        if (descriptor.OwnerType?.StartsWith("MicroflowNode:", StringComparison.OrdinalIgnoreCase) == true)
        {
            parts.Insert(3, $"nodeId={descriptor.OwnerId}");
            parts.Insert(4, $"nodeName={descriptor.OwnerName}");
        }

        return string.Join("; ", parts);
    }

    private static object? TryReadLegacyActualValue(RuntimeValueExpressionDto expression, RuntimeExpressionEvaluationContext context)
    {
        try
        {
            var migrated = RuntimeExpressionLegacyMigrationReader.Read(expression);
            if (migrated.Kind != "resourceRef" || string.IsNullOrWhiteSpace(migrated.ResourceId)) return null;
            var separator = migrated.ResourceId.IndexOf(':');
            if (separator <= 0 || separator == migrated.ResourceId.Length - 1) return null;
            var sourceName = migrated.ResourceId[..separator];
            var path = migrated.ResourceId[(separator + 1)..];
            if (!context.Sources.TryGetValue(sourceName, out var source)) return null;
            return path == "*" ? source : RuntimeExpressionPathReader.Read(source, path);
        }
        catch (ValidationException)
        {
            return null;
        }
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element) return value;
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(item => item.Name, item => NormalizeJsonValue(item.Value), StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }
}
