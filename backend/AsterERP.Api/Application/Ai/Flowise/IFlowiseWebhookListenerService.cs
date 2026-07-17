using AsterERP.Contracts.Ai.Flowise;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseWebhookListenerService
{
    Task<FlowiseWebhookListenerRegistrationDto> RegisterAsync(string chatflowId, CancellationToken cancellationToken);

    Task<bool> UnregisterAsync(string chatflowId, string listenerId, CancellationToken cancellationToken);

    Task StreamAsync(string chatflowId, string listenerId, HttpResponse response, CancellationToken cancellationToken);

    Task<FlowiseWebhookTriggerResponse> TriggerAsync(
        string chatflowId,
        FlowiseWebhookTriggerRequest request,
        string? webhookSecret,
        CancellationToken cancellationToken);
}
