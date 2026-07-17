using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.SqlScripts;

public sealed class ApplicationDataCenterSqlScriptIfElseBlockReader
{
    public bool TryReadFirst(string script, out ApplicationDataCenterSqlScriptIfElseBlock block)
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

            block = ReadBlock(script, index);
            return true;
        }

        block = new ApplicationDataCenterSqlScriptIfElseBlock(0, 0, string.Empty, string.Empty, string.Empty);
        return false;
    }

    private static ApplicationDataCenterSqlScriptIfElseBlock ReadBlock(string script, int ifIndex)
    {
        var openCondition = SkipWhitespace(script, ifIndex + 2);
        if (openCondition >= script.Length || script[openCondition] != '(')
        {
            throw new ValidationException("SQL 脚本 IF 条件缺少左括号", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var closeCondition = FindBalancedClose(script, openCondition, '(', ')');
        var openThen = SkipWhitespace(script, closeCondition + 1);
        if (openThen >= script.Length || script[openThen] != '{')
        {
            throw new ValidationException("SQL 脚本 IF 缺少 THEN 语句块", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var closeThen = FindBalancedClose(script, openThen, '{', '}');
        var elseIndex = SkipWhitespace(script, closeThen + 1);
        if (!IsElseKeyword(script, elseIndex))
        {
            throw new ValidationException("SQL 脚本 IF 缺少 ELSE 语句块", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var openElse = SkipWhitespace(script, elseIndex + 4);
        if (openElse >= script.Length || script[openElse] != '{')
        {
            throw new ValidationException("SQL 脚本 ELSE 缺少语句块", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        var closeElse = FindBalancedClose(script, openElse, '{', '}');
        return new ApplicationDataCenterSqlScriptIfElseBlock(
            ifIndex,
            closeElse - ifIndex + 1,
            script[(openCondition + 1)..closeCondition],
            script[(openThen + 1)..closeThen],
            script[(openElse + 1)..closeElse]);
    }

    private static int FindBalancedClose(string script, int openIndex, char openChar, char closeChar)
    {
        var depth = 0;
        for (var index = openIndex; index < script.Length; index += 1)
        {
            if (IsLineCommentStart(script, index))
            {
                index = ReadLineCommentEnd(script, index) - 1;
                continue;
            }

            if (IsBlockCommentStart(script, index))
            {
                index = ReadBlockCommentEnd(script, index) - 1;
                continue;
            }

            if (script[index] is '\'' or '"')
            {
                index = ReadQuotedEnd(script, index) - 1;
                continue;
            }

            if (script[index] == openChar)
            {
                depth += 1;
                continue;
            }

            if (script[index] != closeChar)
            {
                continue;
            }

            depth -= 1;
            if (depth == 0)
            {
                return index;
            }
        }

        throw new ValidationException("SQL 脚本控制块括号未闭合", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    private static bool IsIfKeyword(string script, int index)
    {
        if (!IsKeyword(script, index, "if"))
        {
            return false;
        }

        var next = SkipWhitespace(script, index + 2);
        if (next < script.Length && script[next] == '(')
        {
            return true;
        }

        if (IsKeyword(script, next, "exists"))
        {
            return false;
        }

        if (IsKeyword(script, next, "not"))
        {
            var afterNot = SkipWhitespace(script, next + 3);
            if (IsKeyword(script, afterNot, "exists"))
            {
                return false;
            }
        }

        return IsIfFollower(script, index + 2);
    }

    private static bool IsElseKeyword(string script, int index) =>
        IsKeyword(script, index, "else");

    private static bool IsKeyword(string script, int index, string keyword)
    {
        if (index < 0 ||
            index + keyword.Length > script.Length ||
            !string.Equals(script.Substring(index, keyword.Length), keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var before = index == 0 ? '\0' : script[index - 1];
        var after = index + keyword.Length >= script.Length ? '\0' : script[index + keyword.Length];
        return !IsIdentifierPart(before) && !IsIdentifierPart(after);
    }

    private static bool IsIfFollower(string script, int index) =>
        index >= script.Length ||
        char.IsWhiteSpace(script[index]) ||
        script[index] == '(';

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
}
