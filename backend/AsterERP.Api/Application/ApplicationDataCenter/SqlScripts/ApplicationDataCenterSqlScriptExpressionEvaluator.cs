using System.Globalization;
using AsterERP.Api.Application.Runtime;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Contracts.Runtime;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed class ApplicationDataCenterSqlScriptExpressionEvaluator(
    ApplicationDataCenterSqlScriptExpressionParser parser,
    RuntimeExpressionFunctionCatalog functionCatalog,
    RuntimeExpressionHelperCatalog expressionFunctions,
    ApplicationDataCenterSqlRbacFunctionEvaluator? rbacFunctionEvaluator = null)
{
    public object? Evaluate(string expression, IReadOnlyDictionary<string, object?> variables) =>
        Evaluate(parser.Parse(expression), variables);

    public object? Evaluate(ApplicationDataCenterSqlScriptExpression expression, IReadOnlyDictionary<string, object?> variables) =>
        expression switch
        {
            ApplicationDataCenterSqlScriptLiteralExpression literal => literal.Value,
            ApplicationDataCenterSqlScriptVariableExpression variable => ResolveVariable(variable, variables),
            ApplicationDataCenterSqlScriptFunctionCallExpression function => EvaluateFunction(function, variables),
            ApplicationDataCenterSqlScriptBinaryExpression binary => EvaluateBinary(binary, variables),
            ApplicationDataCenterSqlScriptBareIdentifierExpression identifier => throw new ValidationException(
                $"SQL 表达式不允许裸标识符或数据库列作为函数参数: {identifier.Identifier}",
                ErrorCodes.ApplicationDataCenterInvalidConfig),
            _ => throw new ValidationException("SQL 表达式类型不支持", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };

    private static object? ResolveVariable(
        ApplicationDataCenterSqlScriptVariableExpression variable,
        IReadOnlyDictionary<string, object?> variables)
    {
        if (!variables.TryGetValue(variable.Name, out var value))
        {
            return null;
        }

        return variable.Path.Count == 0
            ? value
            : RuntimeExpressionPathReader.Read(value, string.Join('.', variable.Path));
    }

    private object? EvaluateFunction(
        ApplicationDataCenterSqlScriptFunctionCallExpression function,
        IReadOnlyDictionary<string, object?> variables)
    {
        var definition = functionCatalog.Resolve(function.QualifiedName, requireNamespace: true, requireSqlEnabled: true);
        ValidateArgumentCount(definition, function.Arguments.Count);
        if (ApplicationDataCenterSqlRbacFunctionEvaluator.IsRbacFunction(function))
        {
            if (rbacFunctionEvaluator is null)
            {
                throw new ValidationException(
                    $"SQL RBAC 函数 {function.QualifiedName} 缺少当前用户上下文",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            return rbacFunctionEvaluator.Evaluate(function, definition);
        }

        var values = function.Arguments.Select(argument => Evaluate(argument, variables)).ToArray();
        var inputOffset = definition.RequiresInput ? 1 : 0;
        var input = definition.RequiresInput ? values.FirstOrDefault() : null;
        var args = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < definition.Parameters.Count; index += 1)
        {
            var valueIndex = index + inputOffset;
            if (valueIndex < values.Length)
            {
                args[definition.Parameters[index].Name] = values[valueIndex];
            }
            else if (definition.Parameters[index].DefaultValue is not null)
            {
                args[definition.Parameters[index].Name] = definition.Parameters[index].DefaultValue;
            }
        }

        AddCompatibilityArgs(args, values, inputOffset);
        return expressionFunctions.Apply(input, new RuntimeExpressionHelperDto
        {
            Args = args,
            Name = definition.CanonicalName
        });
    }

    private static void ValidateArgumentCount(RuntimeExpressionFunctionDefinitionDto definition, int count)
    {
        var min = (definition.RequiresInput ? 1 : 0) + definition.Parameters.Count(item => item.Required);
        var max = (definition.RequiresInput ? 1 : 0) + definition.Parameters.Count;
        if (count < min || count > max)
        {
            throw new ValidationException(
                $"SQL 函数 {definition.QualifiedName} 参数数量错误，应为 {min} 到 {max} 个，实际 {count} 个",
                ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private static void AddCompatibilityArgs(Dictionary<string, object?> args, IReadOnlyList<object?> values, int inputOffset)
    {
        if (values.Count <= inputOffset)
        {
            return;
        }

        var first = values[inputOffset];
        args.TryAdd("value", first);
        args.TryAdd("field", first);
        args.TryAdd("path", first);
        args.TryAdd("separator", first);
        args.TryAdd("count", first);
        args.TryAdd("length", first);
        args.TryAdd("digits", first);
        args.TryAdd("min", first);
        args.TryAdd("start", first);
        args.TryAdd("oldValue", first);
        args.TryAdd("from", first);
        args.TryAdd("symbol", first);
        args.TryAdd("trueText", first);

        if (values.Count <= inputOffset + 1)
        {
            return;
        }

        var second = values[inputOffset + 1];
        args.TryAdd("max", second);
        args.TryAdd("end", second);
        args.TryAdd("newValue", second);
        args.TryAdd("to", second);
        args.TryAdd("replace", second);
        args.TryAdd("falseText", second);

        if (values.Count > inputOffset + 2)
        {
            args.TryAdd("mask", values[inputOffset + 2]);
        }
    }

    private object? EvaluateBinary(
        ApplicationDataCenterSqlScriptBinaryExpression binary,
        IReadOnlyDictionary<string, object?> variables)
    {
        var left = Evaluate(binary.Left, variables);
        var right = Evaluate(binary.Right, variables);
        return binary.Operator switch
        {
            "+" => Add(left, right),
            "-" => ToDecimal(left) - ToDecimal(right),
            "*" => ToDecimal(left) * ToDecimal(right),
            "/" => ToDecimal(right) == 0 ? 0 : ToDecimal(left) / ToDecimal(right),
            "==" => Equals(NormalizeComparable(left), NormalizeComparable(right)),
            "!=" => !Equals(NormalizeComparable(left), NormalizeComparable(right)),
            ">" => Compare(left, right) > 0,
            ">=" => Compare(left, right) >= 0,
            "<" => Compare(left, right) < 0,
            "<=" => Compare(left, right) <= 0,
            _ => throw new ValidationException($"SQL 表达式操作符不支持: {binary.Operator}", ErrorCodes.ApplicationDataCenterInvalidConfig)
        };
    }

    private static object? Add(object? left, object? right) =>
        IsNumeric(left) && IsNumeric(right)
            ? ToDecimal(left) + ToDecimal(right)
            : string.Concat(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture));

    private static int Compare(object? left, object? right)
    {
        if (IsNumeric(left) && IsNumeric(right))
        {
            return ToDecimal(left).CompareTo(ToDecimal(right));
        }

        return string.Compare(
            Convert.ToString(left, CultureInfo.InvariantCulture),
            Convert.ToString(right, CultureInfo.InvariantCulture),
            StringComparison.OrdinalIgnoreCase);
    }

    private static object? NormalizeComparable(object? value) =>
        IsNumeric(value) ? ToDecimal(value) : value;

    private static bool IsNumeric(object? value) =>
        value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal ||
        decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out _);

    private static decimal ToDecimal(object? value) =>
        decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
}
