namespace AsterERP.Contracts.ApplicationDataCenter;

/// <summary>
/// Canonical parameter value types shared by Mapping Cache configuration,
/// execution, and QueryPlan resource resolution.
/// </summary>
public static class ApplicationMappingCacheParameterType
{
    public const string String = "string";
    public const string Number = "number";
    public const string Boolean = "boolean";
    public const string Date = "date";
    public const string Json = "json";

    public static string Normalize(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "string" or "text" or "char" or "varchar" or "nvarchar" or "uuid" or "guid" => String,
            "number" or "int" or "integer" or "smallint" or "bigint" or "long" or "decimal" or "numeric" or "double" or "real" or "float" or "money" => Number,
            "boolean" or "bool" or "bit" => Boolean,
            "date" or "datetime" or "datetime2" or "datetimeoffset" or "timestamp" or "timestamptz" => Date,
            "json" or "jsonb" => Json,
            _ => throw new ArgumentException($"Unsupported Mapping Cache parameter type: {value}.", nameof(value))
        };
    }

    public static string FromColumn(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new ArgumentException("Mapping Cache column data type is required.", nameof(value));
        if (normalized.Contains("json", StringComparison.Ordinal)) return Json;
        if (normalized.Contains("bool", StringComparison.Ordinal) || normalized == "bit") return Boolean;
        if (normalized.Contains("date", StringComparison.Ordinal) || normalized.Contains("time", StringComparison.Ordinal) || normalized.Contains("timestamp", StringComparison.Ordinal)) return Date;
        if (normalized.Contains("int", StringComparison.Ordinal) || normalized.Contains("numeric", StringComparison.Ordinal) || normalized.Contains("decimal", StringComparison.Ordinal) || normalized.Contains("number", StringComparison.Ordinal) || normalized.Contains("double", StringComparison.Ordinal) || normalized.Contains("real", StringComparison.Ordinal) || normalized.Contains("float", StringComparison.Ordinal) || normalized.Contains("money", StringComparison.Ordinal)) return Number;
        return String;
    }

    public static bool IsCompatible(string? columnType, string? parameterType) =>
        string.Equals(FromColumn(columnType), Normalize(parameterType), StringComparison.Ordinal);
}
