using System.Text;
using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

internal static class FlowiseHttpNodeMessageFactory
{
    internal static string NormalizeHttpMethod(string? method)
    {
        var normalized = string.IsNullOrWhiteSpace(method) ? "GET" : method.Trim().ToUpperInvariant();
        return normalized is "GET" or "POST" or "PUT" or "DELETE" or "PATCH" ? normalized : "GET";
    }

    internal static Uri BuildHttpUri(Uri uri, IReadOnlyList<KeyValuePair<string, string>> queryParams)
    {
        if (queryParams.Count == 0)
        {
            return uri;
        }

        var builder = new UriBuilder(uri);
        var existing = string.IsNullOrWhiteSpace(builder.Query) ? string.Empty : builder.Query.TrimStart('?') + "&";
        builder.Query = existing + string.Join("&", queryParams.Select(item => $"{Uri.EscapeDataString(item.Key)}={Uri.EscapeDataString(item.Value)}"));
        return builder.Uri;
    }

    internal static object? BuildHttpResponseData(string responseType, byte[] responseBytes)
    {
        if (responseType.Equals("base64", StringComparison.OrdinalIgnoreCase) ||
            responseType.Equals("arraybuffer", StringComparison.OrdinalIgnoreCase))
        {
            return Convert.ToBase64String(responseBytes);
        }

        var text = Encoding.UTF8.GetString(responseBytes);
        if (!responseType.Equals("json", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return text;
        }
    }

    internal static HttpContent BuildHttpContent(string bodyType, string body)
    {
        if (bodyType.Equals("xWwwFormUrlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var values = TryDeserializeKeyValueBody(body);
            return new FormUrlEncodedContent(values);
        }

        if (bodyType.Equals("formData", StringComparison.OrdinalIgnoreCase))
        {
            var values = TryDeserializeKeyValueBody(body);
            var content = new MultipartFormDataContent();
            foreach (var item in values)
            {
                content.Add(new StringContent(item.Value, Encoding.UTF8), item.Key);
            }

            return content;
        }

        var mediaType = bodyType.Equals("json", StringComparison.OrdinalIgnoreCase)
            ? "application/json"
            : "text/plain";
        return new StringContent(body, Encoding.UTF8, mediaType);
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> TryDeserializeKeyValueBody(string body)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(body);
            return parsed?.ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
