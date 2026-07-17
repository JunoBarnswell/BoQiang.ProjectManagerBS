using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiContextBuilder(ISqlSugarClient db, AiGovernanceService governanceService)
{
    public async Task<IReadOnlyList<ChatMessageContent>> BuildAsync(
        AiConversationEntity conversation,
        AiChatStreamRequest request,
        string? agentPrompt,
        CancellationToken cancellationToken)
    {
        var settings = await governanceService.GetSecuritySettingsAsync(cancellationToken);
        var messages = new List<ChatMessageContent>();
        var systemPrompts = new List<string>();

        if (!string.IsNullOrWhiteSpace(request.PromptTemplateId))
        {
            var template = await db.Queryable<AiPromptTemplateEntity>()
                .FirstAsync(item => item.Id == request.PromptTemplateId && !item.IsDeleted && item.IsEnabled, cancellationToken);
            if (template is not null)
            {
                systemPrompts.Add(template.SystemPrompt);
            }
        }

        if (!string.IsNullOrWhiteSpace(agentPrompt))
        {
            systemPrompts.Add(agentPrompt);
        }

        if (!string.IsNullOrWhiteSpace(conversation.Summary))
        {
            systemPrompts.Add($"以下是历史上下文摘要，请作为背景参考：\n{conversation.Summary}");
        }

        if (systemPrompts.Count > 0)
        {
            messages.Add(new ChatMessageContent(AuthorRole.System, string.Join("\n\n", systemPrompts)));
        }

        var history = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversation.Id)
            .OrderBy(item => item.Seq, OrderByType.Desc)
            .Take(settings.MaxContextMessages)
            .ToListAsync(cancellationToken);

        foreach (var message in history.OrderBy(item => item.Seq))
        {
            if (message.Role is "user" or "assistant")
            {
                messages.Add(new ChatMessageContent(ToAuthorRole(message.Role), message.Content));
            }
        }

        return messages;
    }

    private static AuthorRole ToAuthorRole(string role) =>
        role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? AuthorRole.Assistant :
        role.Equals("system", StringComparison.OrdinalIgnoreCase) ? AuthorRole.System :
        AuthorRole.User;
}
