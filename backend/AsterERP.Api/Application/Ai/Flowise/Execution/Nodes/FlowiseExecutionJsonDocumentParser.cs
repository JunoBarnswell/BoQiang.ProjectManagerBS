using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionJsonDocumentParser
{
    internal JsonDocument ParseElementAsDocument(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            var raw = value.GetString();
            if (string.IsNullOrWhiteSpace(raw))
            {
                return JsonDocument.Parse("[]");
            }

            try
            {
                return JsonDocument.Parse(raw);
            }
            catch (JsonException)
            {
                return JsonDocument.Parse(JsonSerializer.Serialize(new[] { raw.Trim() }));
            }
        }

        return JsonDocument.Parse(value.GetRawText());
    }
}
