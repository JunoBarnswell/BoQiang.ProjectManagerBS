namespace AsterERP.Api.Application.Runtime;

public static class RuntimeExpressionPathWriter
{
    public static void Write(IDictionary<string, object?> target, string? path, object? value)
    {
        var segments = Parse(path);
        if (segments.Count == 0)
        {
            return;
        }

        IDictionary<string, object?> cursor = target;
        for (var index = 0; index < segments.Count; index += 1)
        {
            var segment = segments[index];
            if (index == segments.Count - 1)
            {
                cursor[segment] = value;
                return;
            }

            if (cursor.TryGetValue(segment, out var current) &&
                current is IDictionary<string, object?> nestedDictionary)
            {
                cursor = nestedDictionary;
                continue;
            }

            if (current is IReadOnlyDictionary<string, object?> readOnlyDictionary)
            {
                var clone = new Dictionary<string, object?>(readOnlyDictionary, StringComparer.OrdinalIgnoreCase);
                cursor[segment] = clone;
                cursor = clone;
                continue;
            }

            var next = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            cursor[segment] = next;
            cursor = next;
        }
    }

    private static IReadOnlyList<string> Parse(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return [];
        }

        return path
            .Trim()
            .TrimStart('$')
            .TrimStart('.')
            .Replace("[", ".", StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
