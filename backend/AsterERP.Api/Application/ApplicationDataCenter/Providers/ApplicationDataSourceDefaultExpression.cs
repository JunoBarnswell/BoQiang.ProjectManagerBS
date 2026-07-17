using System.Globalization;
using System.Text.RegularExpressions;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter.Providers;

public sealed record ApplicationDataSourceDefaultExpression(
    ApplicationDataSourceDefaultExpressionKind Kind,
    string Sql)
{
    private static readonly Regex NumericLiteral = new(
        "^[+-]?(?:\\d+(?:\\.\\d*)?|\\.\\d+)(?:[eE][+-]?\\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex QuotedLiteral = new(
        "^'(?:''|[^'])*'$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex FunctionLiteral = new(
        "^[A-Za-z_][A-Za-z0-9_]*\\(\\)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ProviderFunctions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [ApplicationDataSourceType.Sqlite] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME"
            },
            [ApplicationDataSourceType.MySql] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME", "NOW()", "UTC_TIMESTAMP()"
            },
            [ApplicationDataSourceType.PostgreSql] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CURRENT_TIMESTAMP", "CURRENT_DATE", "CURRENT_TIME", "LOCALTIMESTAMP", "NOW()"
            },
            [ApplicationDataSourceType.SqlServer] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CURRENT_TIMESTAMP", "GETDATE()", "GETUTCDATE()", "SYSDATETIME()", "SYSUTCDATETIME()"
            }
        };

    public static ApplicationDataSourceDefaultExpression? Parse(string? rawValue, string providerType)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var value = rawValue.Trim();
        if (string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return new(ApplicationDataSourceDefaultExpressionKind.Null, "NULL");
        }

        if (bool.TryParse(value, out var booleanValue))
        {
            return new(ApplicationDataSourceDefaultExpressionKind.Boolean, booleanValue ? "TRUE" : "FALSE");
        }

        if (NumericLiteral.IsMatch(value))
        {
            return new(ApplicationDataSourceDefaultExpressionKind.Numeric, value);
        }

        if (QuotedLiteral.IsMatch(value))
        {
            var literal = value[1..^1].Replace("''", "'", StringComparison.Ordinal);
            var kind = DateTimeOffset.TryParse(literal, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _)
                ? ApplicationDataSourceDefaultExpressionKind.Date
                : ApplicationDataSourceDefaultExpressionKind.String;
            return new(kind, value);
        }

        if (value.Length > 200 || value.Contains(';', StringComparison.Ordinal) || value.Contains("--", StringComparison.Ordinal) ||
            value.Contains("/*", StringComparison.Ordinal) || value.Contains("*/", StringComparison.Ordinal))
        {
            throw InvalidDefault(value);
        }

        if (IsAllowedFunction(value, providerType))
        {
            return new(ApplicationDataSourceDefaultExpressionKind.Function, value.ToUpperInvariant());
        }

        throw InvalidDefault(value);
    }

    public string RenderFor(string providerType)
    {
        if (Kind != ApplicationDataSourceDefaultExpressionKind.Boolean)
        {
            return Sql;
        }

        return providerType.Equals(ApplicationDataSourceType.SqlServer, StringComparison.OrdinalIgnoreCase)
            ? Sql.Equals("TRUE", StringComparison.OrdinalIgnoreCase) ? "1" : "0"
            : Sql;
    }

    private static bool IsAllowedFunction(string value, string providerType)
    {
        var normalizedProvider = string.Equals(providerType, ApplicationDataSourceType.ApplicationDatabase, StringComparison.OrdinalIgnoreCase)
            ? ApplicationDataSourceType.Sqlite
            : providerType;
        if (!ProviderFunctions.TryGetValue(normalizedProvider, out var allowedFunctions))
        {
            return false;
        }

        return (value.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("CURRENT_DATE", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("CURRENT_TIME", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("LOCALTIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                FunctionLiteral.IsMatch(value)) && allowedFunctions.Contains(value);
    }

    private static ValidationException InvalidDefault(string value) =>
        new($"Default expression is invalid or unsupported by the current provider: {value}", ErrorCodes.ApplicationDataCenterInvalidConfig);
}

public enum ApplicationDataSourceDefaultExpressionKind
{
    Null,
    Numeric,
    Boolean,
    String,
    Date,
    Function
}
