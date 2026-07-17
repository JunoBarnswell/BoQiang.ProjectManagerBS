using System.Globalization;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed class ApplicationDataCenterSqlScriptExpressionTokenizer
{
    public IReadOnlyList<ApplicationDataCenterSqlScriptExpressionToken> Tokenize(string expression)
    {
        var tokens = new List<ApplicationDataCenterSqlScriptExpressionToken>();
        var index = 0;
        while (index < expression.Length)
        {
            var current = expression[index];
            if (char.IsWhiteSpace(current))
            {
                index += 1;
                continue;
            }

            if (current == '@')
            {
                tokens.Add(ReadVariable(expression, index));
                index = tokens[^1].End;
                continue;
            }

            if (current is '\'' or '"')
            {
                tokens.Add(ReadString(expression, index));
                index = tokens[^1].End;
                continue;
            }

            if (char.IsDigit(current) || current == '-' && index + 1 < expression.Length && char.IsDigit(expression[index + 1]))
            {
                tokens.Add(ReadNumber(expression, index));
                index = tokens[^1].End;
                continue;
            }

            if (IsIdentifierStart(current))
            {
                tokens.Add(ReadIdentifier(expression, index));
                index = tokens[^1].End;
                continue;
            }

            if (current == '(')
            {
                tokens.Add(new(ApplicationDataCenterSqlScriptExpressionTokenType.OpenParen, "(", index, index + 1));
                index += 1;
                continue;
            }

            if (current == ')')
            {
                tokens.Add(new(ApplicationDataCenterSqlScriptExpressionTokenType.CloseParen, ")", index, index + 1));
                index += 1;
                continue;
            }

            if (current == ',')
            {
                tokens.Add(new(ApplicationDataCenterSqlScriptExpressionTokenType.Comma, ",", index, index + 1));
                index += 1;
                continue;
            }

            if (current == '.')
            {
                tokens.Add(new(ApplicationDataCenterSqlScriptExpressionTokenType.Dot, ".", index, index + 1));
                index += 1;
                continue;
            }

            if ("+-*/=<>!".Contains(current, StringComparison.Ordinal))
            {
                tokens.Add(ReadOperator(expression, index));
                index = tokens[^1].End;
                continue;
            }

            throw new ValidationException($"SQL 表达式包含不支持的字符: {current}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        tokens.Add(new(ApplicationDataCenterSqlScriptExpressionTokenType.End, string.Empty, expression.Length, expression.Length));
        return tokens;
    }

    private static ApplicationDataCenterSqlScriptExpressionToken ReadVariable(string expression, int start)
    {
        var index = start + 1;
        if (index >= expression.Length || !IsIdentifierStart(expression[index]))
        {
            throw new ValidationException("SQL 表达式变量名无效", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        index += 1;
        while (index < expression.Length && IsIdentifierPart(expression[index]))
        {
            index += 1;
        }

        return new(ApplicationDataCenterSqlScriptExpressionTokenType.Variable, expression[start..index], start, index);
    }

    private static ApplicationDataCenterSqlScriptExpressionToken ReadString(string expression, int start)
    {
        var quote = expression[start];
        var index = start + 1;
        var value = new global::System.Text.StringBuilder();
        while (index < expression.Length)
        {
            var current = expression[index];
            if (current == quote)
            {
                if (quote == '\'' && index + 1 < expression.Length && expression[index + 1] == '\'')
                {
                    value.Append('\'');
                    index += 2;
                    continue;
                }

                return new(ApplicationDataCenterSqlScriptExpressionTokenType.String, value.ToString(), start, index + 1);
            }

            value.Append(current);
            index += 1;
        }

        throw new ValidationException("SQL 表达式字符串未闭合", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static ApplicationDataCenterSqlScriptExpressionToken ReadNumber(string expression, int start)
    {
        var index = start;
        if (expression[index] == '-')
        {
            index += 1;
        }

        while (index < expression.Length && char.IsDigit(expression[index]))
        {
            index += 1;
        }

        if (index < expression.Length && expression[index] == '.')
        {
            index += 1;
            while (index < expression.Length && char.IsDigit(expression[index]))
            {
                index += 1;
            }
        }

        var text = expression[start..index];
        if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
        {
            throw new ValidationException($"SQL 表达式数字无效: {text}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return new(ApplicationDataCenterSqlScriptExpressionTokenType.Number, text, start, index);
    }

    private static ApplicationDataCenterSqlScriptExpressionToken ReadIdentifier(string expression, int start)
    {
        var index = start + 1;
        while (index < expression.Length && IsIdentifierPart(expression[index]))
        {
            index += 1;
        }

        var text = expression[start..index];
        var type = text.ToLowerInvariant() switch
        {
            "true" or "false" => ApplicationDataCenterSqlScriptExpressionTokenType.Boolean,
            "null" => ApplicationDataCenterSqlScriptExpressionTokenType.Null,
            _ => ApplicationDataCenterSqlScriptExpressionTokenType.Identifier
        };
        return new(type, text, start, index);
    }

    private static ApplicationDataCenterSqlScriptExpressionToken ReadOperator(string expression, int start)
    {
        if (start + 1 < expression.Length)
        {
            var twoChars = expression.Substring(start, 2);
            if (twoChars is "==" or "!=" or ">=" or "<=")
            {
                return new(ApplicationDataCenterSqlScriptExpressionTokenType.Operator, twoChars, start, start + 2);
            }
        }

        return new(ApplicationDataCenterSqlScriptExpressionTokenType.Operator, expression[start].ToString(), start, start + 1);
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value == '_';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value == '_';
}
