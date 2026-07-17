using System.Text;
using System.Text.RegularExpressions;
using AsterERP.Api.Application.Runtime.ExpressionFunctions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed partial class ApplicationDataCenterSqlScriptFunctionParameterizer(
    ApplicationDataCenterSqlScriptExpressionParser parser,
    ApplicationDataCenterSqlScriptExpressionEvaluator evaluator)
{
    public ApplicationDataCenterSqlScriptParameterizationResult Parameterize(
        string script,
        Dictionary<string, object?> variables)
    {
        var builder = new StringBuilder(script.Length);
        var generated = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var index = 0;
        var generatedIndex = 0;
        while (index < script.Length)
        {
            if (IsLineCommentStart(script, index))
            {
                var end = ReadLineCommentEnd(script, index);
                builder.Append(script, index, end - index);
                index = end;
                continue;
            }

            if (IsBlockCommentStart(script, index))
            {
                var end = ReadBlockCommentEnd(script, index);
                builder.Append(script, index, end - index);
                index = end;
                continue;
            }

            if (script[index] is '\'' or '"')
            {
                var end = ReadQuotedEnd(script, index);
                builder.Append(script, index, end - index);
                index = end;
                continue;
            }

            if (!IsIdentifierStart(script[index]) ||
                !TryReadRuntimeFunctionStart(script, index, out var namespaceName, out var isKnownNamespace))
            {
                builder.Append(script[index]);
                index += 1;
                continue;
            }

            if (!isKnownNamespace)
            {
                throw new ValidationException(
                    $"SQL 函数命名空间不在白名单内: {namespaceName}",
                    ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var expressionText = script[index..ReadFunctionCallEnd(script, index)];
            var expression = parser.Parse(expressionText);
            if (expression is not ApplicationDataCenterSqlScriptFunctionCallExpression function)
            {
                throw new ValidationException("SQL 函数调用语法无效", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            var parameterName = $"__fn_{++generatedIndex}";
            var value = evaluator.Evaluate(function, variables);
            generated[parameterName] = value;
            variables[parameterName] = value;
            builder.Append('@').Append(parameterName);
            index += expressionText.Length;
        }

        return new(builder.ToString(), generated);
    }

    public void ValidateFunctionCalls(string script)
    {
        ValidateControlExpressions(script);
        var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        _ = Parameterize(script, values);
    }

    private void ValidateControlExpressions(string script)
    {
        foreach (Match match in SetExpressionRegex().Matches(script))
        {
            parser.Parse(match.Groups["expression"].Value);
        }

        foreach (var condition in ExtractIfConditions(script))
        {
            parser.Parse(condition);
        }
    }

    private static bool TryReadRuntimeFunctionStart(string script, int start, out string namespaceName, out bool isKnownNamespace)
    {
        namespaceName = string.Empty;
        isKnownNamespace = false;
        var match = RuntimeFunctionStartRegex().Match(script[start..]);
        if (!match.Success)
        {
            return false;
        }

        namespaceName = match.Groups["namespace"].Value;
        if (!RuntimeExpressionFunctionCatalog.LooksLikeRuntimeFunctionNamespace(namespaceName))
        {
            return false;
        }

        isKnownNamespace = RuntimeExpressionFunctionCatalog.IsKnownNamespace(namespaceName);
        return true;
    }

    private static bool IsLineCommentStart(string script, int index) =>
        index + 1 < script.Length && script[index] == '-' && script[index + 1] == '-';

    private static int ReadLineCommentEnd(string script, int index)
    {
        var end = script.IndexOf('\n', index);
        return end < 0 ? script.Length : end + 1;
    }

    private static bool IsBlockCommentStart(string script, int index) =>
        index + 1 < script.Length && script[index] == '/' && script[index + 1] == '*';

    private static int ReadBlockCommentEnd(string script, int index)
    {
        var end = script.IndexOf("*/", index + 2, StringComparison.Ordinal);
        return end < 0 ? script.Length : end + 2;
    }

    private static int ReadQuotedEnd(string script, int index)
    {
        var quote = script[index];
        index += 1;
        while (index < script.Length)
        {
            if (script[index] == quote)
            {
                if (quote == '\'' && index + 1 < script.Length && script[index + 1] == '\'')
                {
                    index += 2;
                    continue;
                }

                return index + 1;
            }

            index += 1;
        }

        return script.Length;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static IEnumerable<string> ExtractIfConditions(string script)
    {
        var index = 0;
        while (index < script.Length)
        {
            if (IsLineCommentStart(script, index))
            {
                index = ReadLineCommentEnd(script, index);
                continue;
            }

            if (IsBlockCommentStart(script, index))
            {
                index = ReadBlockCommentEnd(script, index);
                continue;
            }

            if (script[index] is '\'' or '"')
            {
                index = ReadQuotedEnd(script, index);
                continue;
            }

            if (!IsIfKeyword(script, index))
            {
                index += 1;
                continue;
            }

            var openParen = SkipWhitespace(script, index + 2);
            if (openParen >= script.Length || script[openParen] != '(')
            {
                index += 2;
                continue;
            }

            var closeParen = FindBalancedCloseParen(script, openParen);
            yield return script[(openParen + 1)..closeParen];
            index = closeParen + 1;
        }
    }

    private static bool IsIfKeyword(string script, int index)
    {
        if (index + 1 >= script.Length ||
            !string.Equals(script.Substring(index, 2), "if", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = index == 0 ? '\0' : script[index - 1];
        var after = index + 2 >= script.Length ? '\0' : script[index + 2];
        return !IsIdentifierPart(before) && (char.IsWhiteSpace(after) || after == '(');
    }

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_';

    private static int SkipWhitespace(string script, int index)
    {
        while (index < script.Length && char.IsWhiteSpace(script[index]))
        {
            index += 1;
        }

        return index;
    }

    private static int FindBalancedCloseParen(string script, int openParen)
    {
        var depth = 0;
        for (var index = openParen; index < script.Length; index += 1)
        {
            if (script[index] is '\'' or '"')
            {
                index = ReadQuotedEnd(script, index) - 1;
                continue;
            }

            if (script[index] == '(')
            {
                depth += 1;
                continue;
            }

            if (script[index] != ')')
            {
                continue;
            }

            depth -= 1;
            if (depth == 0)
            {
                return index;
            }
        }

        throw new ValidationException("SQL 脚本 IF 条件括号未闭合", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static int ReadFunctionCallEnd(string script, int start)
    {
        var openParen = script.IndexOf('(', start);
        if (openParen < 0)
        {
            throw new ValidationException("SQL 函数调用缺少左括号", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return FindBalancedCloseParen(script, openParen) + 1;
    }

    [GeneratedRegex("^(?<namespace>[A-Za-z_][A-Za-z0-9_]*)\\s*\\.\\s*(?<function>[A-Za-z_][A-Za-z0-9_]*)\\s*\\(", RegexOptions.CultureInvariant)]
    private static partial Regex RuntimeFunctionStartRegex();

    [GeneratedRegex("\\bSET\\s+@[A-Za-z_][A-Za-z0-9_]*\\s*=\\s*(?<expression>[^;]+);?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetExpressionRegex();
}
