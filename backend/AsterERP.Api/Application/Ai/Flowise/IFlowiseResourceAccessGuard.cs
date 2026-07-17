using AsterERP.Api.Modules.Ai.Flowise;

namespace AsterERP.Api.Application.Ai.Flowise;

public interface IFlowiseResourceAccessGuard
{
    Task<FlowiseChatFlowEntity> GetChatflowForCurrentWorkspaceAsync(
        string chatflowId,
        CancellationToken cancellationToken = default);

    Task EnsureChatflowForCurrentWorkspaceAsync(
        string chatflowId,
        CancellationToken cancellationToken = default);
}
