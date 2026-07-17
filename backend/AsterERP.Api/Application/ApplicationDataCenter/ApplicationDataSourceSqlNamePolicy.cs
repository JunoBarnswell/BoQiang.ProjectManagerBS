using System.Text.RegularExpressions;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public static class ApplicationDataSourceSqlNamePolicy
{
    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    public static string RequireIdentifier(string value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value) || !IdentifierRegex.IsMatch(value.Trim()))
        {
            throw new ValidationException($"{fieldName}只能包含字母、数字、下划线，且必须以字母或下划线开头", ErrorCodes.ApplicationDataCenterInvalidConfig);
        }

        return value.Trim();
    }

    public static string? OptionalIdentifier(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return RequireIdentifier(value, fieldName);
    }

    public static string QuoteQualified(string sourceType, string? schemaName, string name)
    {
        var normalizedName = RequireIdentifier(name, "对象名称");
        var normalizedSchema = OptionalIdentifier(schemaName, "Schema");
        return string.IsNullOrWhiteSpace(normalizedSchema)
            ? Quote(sourceType, normalizedName)
            : $"{Quote(sourceType, normalizedSchema)}.{Quote(sourceType, normalizedName)}";
    }

    public static string Quote(string sourceType, string name)
    {
        var normalized = RequireIdentifier(name, "标识符");
        return sourceType switch
        {
            ApplicationDataSourceType.MySql => $"`{normalized}`",
            ApplicationDataSourceType.SqlServer => $"[{normalized}]",
            _ => $"\"{normalized}\""
        };
    }
}
