using System.Globalization;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed class ApplicationDataCenterSqlScriptExpressionParser(ApplicationDataCenterSqlScriptExpressionTokenizer tokenizer)
{
    private IReadOnlyList<ApplicationDataCenterSqlScriptExpressionToken> tokens = [];
    private int index;

    public ApplicationDataCenterSqlScriptExpression Parse(string expression)
    {
        var result = ParsePartial(expression);
        if (Current.Type != ApplicationDataCenterSqlScriptExpressionTokenType.End)
        {
            throw new ValidationException($"SQL 表达式存在多余内容: {Current.Text}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return result.Expression;
    }

    public ApplicationDataCenterSqlScriptParseResult ParsePartial(string expression)
    {
        tokens = tokenizer.Tokenize(expression);
        index = 0;
        var parsed = ParseComparison();
        return new(parsed, Current.Start);
    }

    private ApplicationDataCenterSqlScriptExpression ParseComparison()
    {
        var left = ParseAdditive();
        if (Current.Type == ApplicationDataCenterSqlScriptExpressionTokenType.Operator &&
            Current.Text is "==" or "!=" or ">" or ">=" or "<" or "<=")
        {
            var op = Current.Text;
            Advance();
            var right = ParseAdditive();
            return new ApplicationDataCenterSqlScriptBinaryExpression(left, op, right);
        }

        return left;
    }

    private ApplicationDataCenterSqlScriptExpression ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Current.Type == ApplicationDataCenterSqlScriptExpressionTokenType.Operator && Current.Text is "+" or "-")
        {
            var op = Current.Text;
            Advance();
            var right = ParseMultiplicative();
            left = new ApplicationDataCenterSqlScriptBinaryExpression(left, op, right);
        }

        return left;
    }

    private ApplicationDataCenterSqlScriptExpression ParseMultiplicative()
    {
        var left = ParsePrimary();
        while (Current.Type == ApplicationDataCenterSqlScriptExpressionTokenType.Operator && Current.Text is "*" or "/")
        {
            var op = Current.Text;
            Advance();
            var right = ParsePrimary();
            left = new ApplicationDataCenterSqlScriptBinaryExpression(left, op, right);
        }

        return left;
    }

    private ApplicationDataCenterSqlScriptExpression ParsePrimary()
    {
        var token = Current;
        switch (token.Type)
        {
            case ApplicationDataCenterSqlScriptExpressionTokenType.Variable:
                Advance();
                return ParseVariable(token);
            case ApplicationDataCenterSqlScriptExpressionTokenType.String:
                Advance();
                return new ApplicationDataCenterSqlScriptLiteralExpression(token.Text);
            case ApplicationDataCenterSqlScriptExpressionTokenType.Number:
                Advance();
                return new ApplicationDataCenterSqlScriptLiteralExpression(decimal.Parse(token.Text, CultureInfo.InvariantCulture));
            case ApplicationDataCenterSqlScriptExpressionTokenType.Boolean:
                Advance();
                return new ApplicationDataCenterSqlScriptLiteralExpression(bool.Parse(token.Text));
            case ApplicationDataCenterSqlScriptExpressionTokenType.Null:
                Advance();
                return new ApplicationDataCenterSqlScriptLiteralExpression(null);
            case ApplicationDataCenterSqlScriptExpressionTokenType.Identifier:
                return ParseIdentifierExpression();
            case ApplicationDataCenterSqlScriptExpressionTokenType.OpenParen:
                Advance();
                var inner = ParseComparison();
                Expect(ApplicationDataCenterSqlScriptExpressionTokenType.CloseParen, ")");
                return inner;
            default:
                throw new ValidationException($"SQL 表达式语法错误: {token.Text}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }
    }

    private ApplicationDataCenterSqlScriptExpression ParseVariable(ApplicationDataCenterSqlScriptExpressionToken variableToken)
    {
        var path = new List<string>();
        while (Current.Type == ApplicationDataCenterSqlScriptExpressionTokenType.Dot)
        {
            Advance();
            var part = Expect(ApplicationDataCenterSqlScriptExpressionTokenType.Identifier, "字段路径");
            path.Add(part.Text);
        }

        return new ApplicationDataCenterSqlScriptVariableExpression(variableToken.Text.TrimStart('@'), path);
    }

    private ApplicationDataCenterSqlScriptExpression ParseIdentifierExpression()
    {
        var first = Expect(ApplicationDataCenterSqlScriptExpressionTokenType.Identifier, "标识符");
        if (Current.Type != ApplicationDataCenterSqlScriptExpressionTokenType.Dot)
        {
            return new ApplicationDataCenterSqlScriptBareIdentifierExpression(first.Text);
        }

        Advance();
        var second = Expect(ApplicationDataCenterSqlScriptExpressionTokenType.Identifier, "函数名");
        if (Current.Type != ApplicationDataCenterSqlScriptExpressionTokenType.OpenParen)
        {
            return new ApplicationDataCenterSqlScriptBareIdentifierExpression($"{first.Text}.{second.Text}");
        }

        Advance();
        var args = new List<ApplicationDataCenterSqlScriptExpression>();
        if (Current.Type != ApplicationDataCenterSqlScriptExpressionTokenType.CloseParen)
        {
            while (true)
            {
                args.Add(ParseComparison());
                if (Current.Type != ApplicationDataCenterSqlScriptExpressionTokenType.Comma)
                {
                    break;
                }

                Advance();
            }
        }

        Expect(ApplicationDataCenterSqlScriptExpressionTokenType.CloseParen, ")");
        return new ApplicationDataCenterSqlScriptFunctionCallExpression(first.Text, second.Text, args);
    }

    private ApplicationDataCenterSqlScriptExpressionToken Expect(ApplicationDataCenterSqlScriptExpressionTokenType type, string label)
    {
        if (Current.Type != type)
        {
            throw new ValidationException($"SQL 表达式缺少 {label}", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var token = Current;
        Advance();
        return token;
    }

    private ApplicationDataCenterSqlScriptExpressionToken Current => tokens[Math.Min(index, tokens.Count - 1)];

    private void Advance()
    {
        if (index < tokens.Count - 1)
        {
            index += 1;
        }
    }
}
