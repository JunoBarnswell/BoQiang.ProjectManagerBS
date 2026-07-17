using System.Text.Json;

namespace AsterERP.Api.Application.Runtime;

public static class RuntimeExpressionPathReader
{
    public static object? Read(object? source, string? path)
    {
        if (source is null || string.IsNullOrWhiteSpace(path))
        {
            return source;
        }

        object? current = source;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            current = ReadSegment(current, segment);
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }

    private static object? ReadSegment(object? source, string segment)
    {
        if (source is null)
        {
            return null;
        }

        if (source is JsonElement element)
        {
            return ReadJsonSegment(element, segment);
        }

        if (source is IReadOnlyDictionary<string, object?> readOnlyDictionary)
        {
            return readOnlyDictionary.TryGetValue(segment, out var value) ? value : null;
        }

        if (source is IDictionary<string, object?> dictionary)
        {
            return dictionary.TryGetValue(segment, out var value) ? value : null;
        }

        if (source is global::System.Collections.IList list && int.TryParse(segment.Trim('[', ']'), out var index))
        {
            return index >= 0 && index < list.Count ? list[index] : null;
        }

        var property = source.GetType().GetProperty(segment);
        return property?.GetValue(source);
    }

    private static object? ReadJsonSegment(JsonElement element, string segment)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(segment, out var property))
        {
            return property;
        }

        if (element.ValueKind == JsonValueKind.Array &&
            int.TryParse(segment.Trim('[', ']'), out var index) &&
            index >= 0 &&
            index < element.GetArrayLength())
        {
            return element.EnumerateArray().ElementAt(index);
        }

        return null;
    }
}
