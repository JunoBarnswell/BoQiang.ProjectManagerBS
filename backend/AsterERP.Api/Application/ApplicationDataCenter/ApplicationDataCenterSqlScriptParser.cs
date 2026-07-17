using System.Text.RegularExpressions;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed partial class ApplicationDataCenterSqlScriptParser
{
    private static readonly string[] ForbiddenKeywords =
    [
        "alter",
        "attach",
        "call",
        "detach",
        "exec",
        "execute",
        "grant",
        "merge",
        "pragma",
        "replace",
        "revoke",
        "vacuum"
    ];

    public ApplicationDataCenterSqlScriptPlan Parse(string script)
    {
        var normalized = script.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 20000)
        {
            throw new ValidationException("SQL 脚本不能为空且长度不能超过 20000", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        RejectDangerousStatements(normalized);
        var declaredNames = ExtractDeclaredVariableNames(normalized);
        var returnSelect = ReturnSelectRegex().Match(normalized);
        if (returnSelect.Success)
        {
            return new ApplicationDataCenterSqlScriptPlan
            {
                DeclaredVariableNames = declaredNames,
                OriginalScript = normalized,
                ReturnKind = "select",
                ReturnSql = $"SELECT {TrimTrailingStatement(returnSelect.Groups["sql"].Value)}",
                SqlStatements = SplitSqlStatements(normalized[..returnSelect.Index])
            };
        }

        var returnVariable = ReturnVariableRegex().Match(normalized);
        if (returnVariable.Success)
        {
            return new ApplicationDataCenterSqlScriptPlan
            {
                DeclaredVariableNames = declaredNames,
                OriginalScript = normalized,
                ReturnKind = "variable",
                ReturnVariableName = returnVariable.Groups["name"].Value,
                SqlStatements = SplitSqlStatements(normalized[..returnVariable.Index])
            };
        }

        var returnJson = ReturnJsonRegex().Match(normalized);
        if (returnJson.Success)
        {
            return new ApplicationDataCenterSqlScriptPlan
            {
                DeclaredVariableNames = declaredNames,
                OriginalScript = normalized,
                ReturnKind = "json",
                ReturnJson = TrimTrailingStatement(returnJson.Groups["json"].Value),
                SqlStatements = SplitSqlStatements(normalized[..returnJson.Index])
            };
        }

        throw new ValidationException("SQL 脚本必须显式 RETURN SELECT、RETURN @变量 或 RETURN JSON", ErrorCodes.ApplicationDataCenterInvalidConfig);
    }

    public IReadOnlySet<string> ExtractReferencedVariableNames(string script)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in VariableRegex().Matches(RemoveStringLiteralsAndComments(script)))
        {
            names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    public IReadOnlySet<string> ExtractDeclaredVariableNames(string script)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in DeclareRegex().Matches(RemoveStringLiteralsAndComments(script)))
        {
            names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    public static string TrimTrailingStatement(string value)
    {
        var normalized = value.Trim();
        while (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized;
    }

    public static IReadOnlyList<string> SplitSqlStatements(string script)
    {
        var statements = new List<string>();
        var current = new global::System.Text.StringBuilder();
        var inString = false;
        for (var index = 0; index < script.Length; index += 1)
        {
            var currentChar = script[index];
            if (currentChar == '\'' && (index == 0 || script[index - 1] != '\\'))
            {
                inString = !inString;
            }

            if (currentChar == ';' && !inString)
            {
                AddStatement(statements, current.ToString());
                current.Clear();
                continue;
            }

            current.Append(currentChar);
        }

        AddStatement(statements, current.ToString());
        return statements;
    }

    public static string RemoveStringLiteralsAndComments(string script)
    {
        var withoutString = Regex.Replace(script, "'(?:''|[^'])*'", "''", RegexOptions.CultureInvariant);
        var withoutLineComment = Regex.Replace(withoutString, "--.*?$", string.Empty, RegexOptions.Multiline | RegexOptions.CultureInvariant);
        return Regex.Replace(withoutLineComment, "/\\*[\\s\\S]*?\\*/", string.Empty, RegexOptions.CultureInvariant);
    }

    private static void AddStatement(List<string> statements, string statement)
    {
        var normalized = TrimTrailingStatement(statement);
        if (!string.IsNullOrWhiteSpace(normalized))
        {
            statements.Add(normalized);
        }
    }

    private static void RejectDangerousStatements(string script)
    {
        var inspectScript = RemoveStringLiteralsAndComments(script);
        foreach (var keyword in ForbiddenKeywords)
        {
            if (Regex.IsMatch(inspectScript, $@"\b{keyword}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new ValidationException($"SQL 脚本不能包含 {keyword.ToUpperInvariant()} 等危险语句", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }

        foreach (var statement in SplitSqlStatements(inspectScript))
        {
            var normalized = statement.Trim();
            if (Regex.IsMatch(normalized, @"^\s*CREATE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(normalized, @"^\s*CREATE\s+(TEMP|TEMPORARY)\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new ValidationException("SQL 脚本只允许创建临时表，不能创建永久表或其他数据库对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }

            if (Regex.IsMatch(normalized, @"^\s*DROP\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(normalized, @"^\s*DROP\s+(TEMP|TEMPORARY)\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) &&
                !Regex.IsMatch(normalized, @"^\s*DROP\s+TABLE\s+IF\s+EXISTS\s+temp\.[A-Za-z_][A-Za-z0-9_]*\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                throw new ValidationException("SQL 脚本只允许删除临时表，不能删除永久表或其他数据库对象", ErrorCodes.ApplicationDataCenterInvalidConfig);
            }
        }
    }

    [GeneratedRegex("\\bDECLARE\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DeclareRegex();

    [GeneratedRegex("(?<!@)@(?<name>[A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant)]
    private static partial Regex VariableRegex();

    [GeneratedRegex("\\bRETURN\\s+SELECT\\s+(?<sql>[\\s\\S]+?)(?:;|\\}|\\s*$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnSelectRegex();

    [GeneratedRegex("\\bRETURN\\s+@(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*(?:;|\\}|\\s*$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnVariableRegex();

    [GeneratedRegex("\\bRETURN\\s+JSON\\s+(?<json>[\\s\\S]+?)(?:;|\\s*$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReturnJsonRegex();
}
