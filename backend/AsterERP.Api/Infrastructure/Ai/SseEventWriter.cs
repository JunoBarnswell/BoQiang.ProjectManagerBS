using System.Text.Json;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Infrastructure.Ai;

public sealed class SseEventWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        HttpResponse response,
        AiStreamEventDto streamEvent,
        CancellationToken cancellationToken)
    {
        if (!response.HasStarted)
        {
            response.ContentType = "text/event-stream; charset=utf-8";
            response.Headers.CacheControl = "no-cache";
            response.Headers.Connection = "keep-alive";
        }

        var payload = JsonSerializer.Serialize(streamEvent, JsonOptions);
        await response.WriteAsync($"event: {streamEvent.Event}\n", cancellationToken);
        await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
