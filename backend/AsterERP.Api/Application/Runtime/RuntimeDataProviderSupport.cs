using System.Globalization;
using SqlSugar;

namespace AsterERP.Api.Application.Runtime;

internal static class RuntimeDataProviderSupport
{
    public static object? CoerceValue(object? value, string dataType)
    {
        if (value is null)
        {
            return null;
        }

        var text = value.ToString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return dataType.ToLowerInvariant() switch
        {
            "number" or "int" or "integer" => int.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var intValue) ? intValue : null,
            "decimal" => decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out var decimalValue) ? decimalValue : null,
            "boolean" or "bool" => bool.TryParse(text, out var boolValue) ? boolValue : null,
            "date" or "datetime" => DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dateValue) ? dateValue : null,
            _ => text
        };
    }

    public static OrderByType ToOrderByType(string order) =>
        string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase)
            ? OrderByType.Desc
            : OrderByType.Asc;
}
