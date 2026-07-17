using System.Text;
using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataSourceSqlPolicy
{
    private static readonly string[] ForbiddenStatementKeywords =
        ["alter", "call", "create", "delete", "drop", "exec", "execute", "grant", "insert", "merge", "replace", "revoke", "truncate", "update"];

    public static string RequireSelectSql(string sql, string fieldName = "SQL")
    {
        var normalized = sql?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20000)
            throw Invalid(fieldName, "必须为非空且长度不能超过 20000");
        if (ContainsTopLevelSemicolon(normalized))
            throw Invalid(fieldName, "不能包含分号或多条语句");

        var inspected = RemoveStringsAndComments(normalized);
        var forbiddenInspection = RemoveStringsOnly(normalized);
        var firstToken = Regex.Match(inspected, "^\\s*([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant).Groups[1].Value;
        if (!firstToken.Equals("SELECT", StringComparison.OrdinalIgnoreCase) && !firstToken.Equals("WITH", StringComparison.OrdinalIgnoreCase))
            throw Invalid(fieldName, "必须是 SELECT 查询");
        foreach (var keyword in ForbiddenStatementKeywords)
        {
            if (Regex.IsMatch(forbiddenInspection, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw Invalid(fieldName, $"只允许只读查询，不能包含 {keyword.ToUpperInvariant()} 语句");
        }

        return normalized;
    }

    private static ValidationException Invalid(string fieldName, string message) =>
        new($"{fieldName}{message}", ErrorCodes.ApplicationDataCenterInvalidConfig);

    private static bool ContainsTopLevelSemicolon(string sql)
    {
        var quote = '\0';
        var blockComment = false;
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';
            if (blockComment)
            {
                if (current == '*' && next == '/') { blockComment = false; index++; }
                continue;
            }
            if (quote != '\0')
            {
                if (current == quote && next == quote) { index++; continue; }
                if (current == quote) quote = '\0';
                continue;
            }
            if (current is '\'' or '"' or '`') { quote = current; continue; }
            if (current == '-' && next == '-') { while (index < sql.Length && sql[index] is not '\r' and not '\n') index++; continue; }
            if (current == '/' && next == '*') { blockComment = true; index++; continue; }
            if (current == ';') return true;
        }
        return false;
    }

    private static string RemoveStringsOnly(string sql)
    {
        var result = new StringBuilder(sql.Length);
        var quote = '\0';
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';
            if (quote != '\0')
            {
                if (current == quote && next == quote) { index++; continue; }
                if (current == quote) quote = '\0';
                continue;
            }
            if (current is '\'' or '"' or '`') { quote = current; result.Append(' '); continue; }
            result.Append(current);
        }
        return result.ToString();
    }

    private static string RemoveStringsAndComments(string sql)
    {
        var result = new StringBuilder(sql.Length);
        var quote = '\0';
        var blockComment = false;
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            var next = index + 1 < sql.Length ? sql[index + 1] : '\0';
            if (blockComment)
            {
                if (current == '*' && next == '/') { blockComment = false; index++; result.Append(' '); }
                continue;
            }
            if (quote != '\0')
            {
                if (current == quote && next == quote) { index++; continue; }
                if (current == quote) quote = '\0';
                continue;
            }
            if (current is '\'' or '"' or '`') { quote = current; result.Append(' '); continue; }
            if (current == '-' && next == '-') { while (index < sql.Length && sql[index] is not '\r' and not '\n') index++; result.Append(' '); continue; }
            if (current == '/' && next == '*') { blockComment = true; index++; result.Append(' '); continue; }
            result.Append(current);
        }
        return result.ToString();
    }
}
