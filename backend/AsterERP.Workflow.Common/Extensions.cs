namespace AsterERP.Workflow.Common;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());
        var attribute = (System.ComponentModel.DescriptionAttribute?)
            Attribute.GetCustomAttribute(field!, typeof(System.ComponentModel.DescriptionAttribute));
        return attribute?.Description ?? value.ToString();
    }
}

public static class CollectionExtensions
{
    public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> source)
    {
        return source.ToList().AsReadOnly();
    }

    public static TValue? GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key)
        where TKey : notnull
    {
        return dictionary.TryGetValue(key, out var value) ? value : default;
    }
}

public static class StringExtensions
{
    public static bool IsBlank(this string? value)
    {
        return string.IsNullOrWhiteSpace(value);
    }

    public static bool IsNotBlank(this string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string ToCamelCase(this string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }
}
