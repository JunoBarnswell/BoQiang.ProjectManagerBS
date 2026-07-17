using System.Text.Json;
using System.Text.Json.Nodes;

namespace AsterERP.Api.Application.System.Printing;

internal static class PrintJsonNodeMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static string? Serialize(object? value)
    {
        return value switch
        {
            null => null,
            JsonNode node => node.ToJsonString(JsonOptions),
            JsonElement element => element.GetRawText(),
            _ => JsonSerializer.Serialize(value, JsonOptions)
        };
    }

    public static JsonNode? Deserialize(string? json)
    {
        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonNode.Parse(json);
    }

    public static JsonObject ToObjectNode(object? value)
    {
        return value switch
        {
            JsonObject jsonObject => (JsonObject)jsonObject.DeepClone(),
            JsonNode jsonNode when jsonNode is not null => JsonNode.Parse(jsonNode.ToJsonString(JsonOptions))?.AsObject() ?? new JsonObject(),
            JsonElement element when element.ValueKind == JsonValueKind.Object => JsonNode.Parse(element.GetRawText())?.AsObject() ?? new JsonObject(),
            _ => JsonSerializer.SerializeToNode(value, JsonOptions)?.AsObject() ?? new JsonObject()
        };
    }

    public static JsonArray ToArrayNode(IEnumerable<IReadOnlyDictionary<string, object?>> rows)
    {
        var result = new JsonArray();
        foreach (var row in rows)
        {
            result.Add(JsonSerializer.SerializeToNode(row, JsonOptions));
        }

        return result;
    }

    public static long ToUnixMilliseconds(DateTime createdTime, DateTime? updatedTime = null)
    {
        var value = updatedTime ?? createdTime;
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
