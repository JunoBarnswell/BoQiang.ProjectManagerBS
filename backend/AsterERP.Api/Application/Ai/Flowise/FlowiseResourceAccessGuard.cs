using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseResourceAccessGuard(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext) : IFlowiseResourceAccessGuard
{
    public async Task<FlowiseChatFlowEntity> GetChatflowForCurrentWorkspaceAsync(
        string chatflowId,
        CancellationToken cancellationToken = default)
    {
        var normalizedChatflowId = NormalizeChatflowId(chatflowId);
        var workspace = workspaceContext.Resolve();

        // Tenant/App are explicit context boundaries. Owner, SharedWorkspace and ViewAll
        // visibility is applied by DataPermissionFilterRegistrar in the SQL query filter.
        var chatflow = await db.Queryable<FlowiseChatFlowEntity>()
            .FirstAsync(
                item => !item.IsDeleted &&
                        item.Id == normalizedChatflowId &&
                        item.TenantId == workspace.TenantId &&
                        item.AppCode == workspace.AppCode,
                cancellationToken);

        return chatflow ?? throw new ValidationException("Flowise ChatFlow 不存在或无权访问", ErrorCodes.PermissionDenied);
    }

    public async Task EnsureChatflowForCurrentWorkspaceAsync(
        string chatflowId,
        CancellationToken cancellationToken = default)
    {
        await GetChatflowForCurrentWorkspaceAsync(chatflowId, cancellationToken);
    }

    private static string NormalizeChatflowId(string chatflowId)
    {
        if (string.IsNullOrWhiteSpace(chatflowId))
        {
            throw new ValidationException("缺少 Flowise ChatFlow Id", ErrorCodes.ParameterInvalid);
        }

        return chatflowId.Trim();
    }
}
