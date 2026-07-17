using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai;

public interface IAiStreamService
{
    Task StreamAsync(string conversationId, AiChatStreamRequest request, HttpResponse response, string traceId, CancellationToken cancellationToken = default);

    Task<bool> StopRunAsync(string runId, CancellationToken cancellationToken = default);
}
