using System.Collections;
using System.Globalization;
using System.Text.Json;
using AsterERP.Contracts.ApplicationDataCenter;

namespace AsterERP.Api.Application.ApplicationDataCenter;

public sealed class ApplicationDataCenterSqlScriptResultProjector
{
    private const int MaxNestedDepth = 3;

    public ApplicationDataCenterPreviewResponse BuildPreview(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        int pageIndex,
        int pageSize,
        string message,
        ApplicationDataCenterSqlScriptAuditSummaryResponse? audit = null)
    {
        var normalizedRows = rows.Select(NormalizeRow).ToArray();
        var totalRows = normalizedRows.Length;
        var boundedPageSize = Math.Clamp(pageSize, 1, 200);
        var boundedPageIndex = Math.Max(1, pageIndex);
        var pageRows = normalizedRows
            .Skip((boundedPageIndex - 1) * boundedPageSize)
            .Take(boundedPageSize)
            .ToArray();
        var fields = InferFields(normalizedRows, string.Empty, 0);
        var datasets = BuildDatasets(pageRows, fields, totalRows);
        var page = new ApplicationDataCenterPreviewPageResponse(
            boundedPageIndex,
            boundedPageSize,
            totalRows,
            boundedPageIndex > 1,
            boundedPageIndex * boundedPageSize < totalRows);

        return new ApplicationDataCenterPreviewResponse(pageRows, fields, message, datasets, page, audit);
    }

    public static IReadOnlyDictionary<string, object?> NormalizeRow(object? value)
    {
        if (!IsDictionary(value))
        {
            value = NormalizeJsonLikeValue(value);
        }

        if (value is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.ToDictionary(
                item => item.Key,
                item => NormalizeJsonLikeValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary<string, object?> dictionary)
        {
            return dictionary.ToDictionary(
                item => item.Key,
                item => NormalizeJsonLikeValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        if (value is IDictionary nonGenericDictionary)
        {
            return nonGenericDictionary
                .Cast<DictionaryEntry>()
                .ToDictionary(
                    item => item.Key?.ToString() ?? string.Empty,
                    item => NormalizeJsonLikeValue(item.Value),
                    StringComparer.OrdinalIgnoreCase);
        }

        if (IsScalar(value))
        {
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["value"] = value
            };
        }

        return value?
            .GetType()
            .GetProperties()
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToDictionary(
                property => property.Name,
                property => NormalizeJsonLikeValue(property.GetValue(value)),
                StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static object? NormalizeJsonLikeValue(object? value)
    {
        value = NormalizeJsonValue(value);
        if (value is string text)
        {
            var trimmed = text.Trim();
            if ((trimmed.StartsWith('{') && trimmed.EndsWith('}')) ||
                (trimmed.StartsWith('[') && trimmed.EndsWith(']')))
            {
                try
                {
                    return NormalizeJsonValue(JsonSerializer.Deserialize<JsonElement>(trimmed));
                }
                catch (JsonException)
                {
                    return value;
                }
            }
        }

        if (IsDictionary(value))
        {
            return NormalizeRow(value);
        }

        if (value is IEnumerable enumerable and not string)
        {
            return enumerable.Cast<object?>().Select(NormalizeJsonLikeValue).ToArray();
        }

        return value;
    }

    public static string DescribeValue(object? value)
    {
        value = NormalizeJsonLikeValue(value);
        if (value is null)
        {
            return "null";
        }

        if (value is string text)
        {
            return text.Length > 120 ? text[..120] + "..." : text;
        }

        if (IsScalar(value))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        if (IsDictionary(value))
        {
            return $"对象({CountDictionaryItems(value)})";
        }

        if (value is IEnumerable enumerable and not string)
        {
            return $"数组({CountEnumerableItems(enumerable)})";
        }

        return value.ToString() ?? string.Empty;
    }

    private static IReadOnlyList<ApplicationDataCenterPreviewDatasetResponse> BuildDatasets(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> pageRows,
        IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> fields,
        int totalRows)
    {
        var datasets = new List<ApplicationDataCenterPreviewDatasetResponse>
        {
            new("main", "主结果", "rows", pageRows, fields, totalRows, false)
        };

        foreach (var field in fields.Where(item => item.ValueKind is "object" or "array" or "arrayObject"))
        {
            var childRows = new List<IReadOnlyDictionary<string, object?>>();
            for (var rowIndex = 0; rowIndex < pageRows.Count; rowIndex += 1)
            {
                if (!pageRows[rowIndex].TryGetValue(field.FieldCode, out var value))
                {
                    continue;
                }

                foreach (var childRow in ToDatasetRows(value))
                {
                    var row = new Dictionary<string, object?>(childRow, StringComparer.OrdinalIgnoreCase)
                    {
                        ["__parentRowIndex"] = rowIndex + 1
                    };
                    childRows.Add(row);
                }
            }

            if (childRows.Count == 0)
            {
                continue;
            }

            datasets.Add(new ApplicationDataCenterPreviewDatasetResponse(
                field.DatasetKey ?? $"main.{field.FieldCode}",
                field.FieldName,
                field.SourcePath ?? field.FieldCode,
                childRows,
                InferFields(childRows, field.FieldCode, 1),
                childRows.Count,
                false,
                field.ValueKind));
        }

        return datasets;
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> ToDatasetRows(object? value)
    {
        value = NormalizeJsonLikeValue(value);
        if (value is null)
        {
            return [];
        }

        if (value is IEnumerable enumerable and not string && !IsDictionary(value))
        {
            return enumerable.Cast<object?>().Select(NormalizeRow).ToArray();
        }

        return [NormalizeRow(value)];
    }

    private static IReadOnlyList<ApplicationDataCenterPreviewFieldResponse> InferFields(
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string parentPath,
        int depth)
    {
        var fieldCodes = rows.SelectMany(row => row.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return fieldCodes
            .Select((fieldCode, index) => CreateField(fieldCode, rows, parentPath, depth, index + 1))
            .ToArray();
    }

    private static ApplicationDataCenterPreviewFieldResponse CreateField(
        string fieldCode,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows,
        string parentPath,
        int depth,
        int order)
    {
        var values = rows.Select(row => row.TryGetValue(fieldCode, out var value) ? NormalizeJsonLikeValue(value) : null).ToArray();
        var valueKind = InferValueKind(values);
        var sourcePath = string.IsNullOrWhiteSpace(parentPath) ? fieldCode : $"{parentPath}.{fieldCode}";
        var children = valueKind is "object" or "arrayObject" && depth < MaxNestedDepth
            ? InferFields(CollectChildRows(values), sourcePath, depth + 1)
            : [];

        return new ApplicationDataCenterPreviewFieldResponse(
            fieldCode,
            fieldCode,
            InferDataType(values, valueKind),
            true,
            string.Equals(fieldCode, "id", StringComparison.OrdinalIgnoreCase),
            order,
            valueKind,
            children,
            valueKind is "object" or "array" or "arrayObject" ? $"main.{sourcePath}" : null,
            sourcePath);
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, object?>> CollectChildRows(IEnumerable<object?> values)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        foreach (var value in values)
        {
            rows.AddRange(ToDatasetRows(value));
        }

        return rows;
    }

    private static string InferValueKind(IEnumerable<object?> values)
    {
        var firstValue = values.Select(NormalizeJsonLikeValue).FirstOrDefault(value => value is not null);
        if (firstValue is null)
        {
            return "scalar";
        }

        if (IsDictionary(firstValue))
        {
            return "object";
        }

        if (firstValue is IEnumerable enumerable and not string)
        {
            foreach (var item in enumerable)
            {
                var normalized = NormalizeJsonLikeValue(item);
                if (normalized is not null)
                {
                    return IsDictionary(normalized) ? "arrayObject" : "array";
                }
            }

            return "array";
        }

        return "scalar";
    }

    private static string InferDataType(IEnumerable<object?> values, string valueKind)
    {
        if (valueKind is "object" or "array" or "arrayObject")
        {
            return valueKind;
        }

        var firstValue = values.FirstOrDefault(value => value is not null);
        return firstValue switch
        {
            null => "Text",
            bool => "Boolean",
            byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => "Number",
            DateTime or DateTimeOffset => "DateTime",
            _ => "Text"
        };
    }

    private static object? NormalizeJsonValue(object? value)
    {
        if (value is not JsonElement element)
        {
            return value;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(
                    item => item.Name,
                    item => NormalizeJsonValue(item.Value),
                    StringComparer.OrdinalIgnoreCase),
            JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToArray(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var longValue) ? longValue : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    private static bool IsDictionary(object? value) =>
        value is IReadOnlyDictionary<string, object?> ||
        value is IDictionary<string, object?> ||
        value is IDictionary;

    private static bool IsScalar(object? value) =>
        value is null ||
        value is string ||
        value is bool ||
        value is char ||
        value is DateTime ||
        value is DateTimeOffset ||
        value.GetType().IsPrimitive ||
        value is decimal;

    private static int CountDictionaryItems(object value) =>
        value switch
        {
            IReadOnlyDictionary<string, object?> readOnlyDictionary => readOnlyDictionary.Count,
            IDictionary<string, object?> dictionary => dictionary.Count,
            IDictionary nonGenericDictionary => nonGenericDictionary.Count,
            _ => 0
        };

    private static int CountEnumerableItems(IEnumerable enumerable)
    {
        if (enumerable is ICollection collection)
        {
            return collection.Count;
        }

        var count = 0;
        foreach (var _ in enumerable)
        {
            count += 1;
        }

        return count;
    }
}
