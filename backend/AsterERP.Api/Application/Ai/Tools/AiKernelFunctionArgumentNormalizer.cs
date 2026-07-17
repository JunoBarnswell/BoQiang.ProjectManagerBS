using System.Text.Json;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;

namespace AsterERP.Api.Application.Ai.Tools;

public sealed class AiKernelFunctionArgumentNormalizer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public (Dictionary<string, object?> Arguments, string ArgumentsJson, IReadOnlyList<string> Issues) Normalize(
        AiKernelFunctionDefinition definition,
        AiToolInvokeRequest request)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.ArgumentsJson))
        {
            Merge(arguments, DeserializeObject(request.ArgumentsJson));
        }

        Merge(arguments, request.Arguments);
        var issues = definition.RequiredArgumentNames
            .Where(name => !arguments.TryGetValue(name, out var value) || IsEmpty(value))
            .Select(name => $"缺少必填参数：{name}")
            .ToList();

        return (arguments, JsonSerializer.Serialize(arguments, JsonOptions), issues);
    }

    public Dictionary<string, object?> DeserializeObject(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions)
                   ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            throw new ValidationException($"工具参数 JSON 格式不正确：{ex.Message}", ErrorCodes.ParameterInvalid);
        }
    }

    private static void Merge(IDictionary<string, object?> target, IReadOnlyDictionary<string, object?> source)
    {
        foreach (var (key, value) in source)
        {
            if (!string.IsNullOrWhiteSpace(key))
            {
                target[key.Trim()] = NormalizeValue(value);
            }
        }
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number when element.TryGetInt64(out var longValue) => longValue,
                JsonValueKind.Number when element.TryGetDecimal(out var decimalValue) => decimalValue,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Object => JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText(), JsonOptions),
                JsonValueKind.Array => JsonSerializer.Deserialize<List<object?>>(element.GetRawText(), JsonOptions),
                JsonValueKind.Null => null,
                _ => element.GetRawText()
            };
        }

        return value;
    }

    private static bool IsEmpty(object? value) =>
        value is null || value is string text && string.IsNullOrWhiteSpace(text);
}
