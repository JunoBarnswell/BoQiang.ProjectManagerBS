using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AsterERP.Api.Application.Ai;

public sealed class AiPlanGenerationService(
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    AiPlanParser parser,
    IAiTaskPlanService taskPlanService)
{
    public async Task<AiTaskPlanDto> GenerateAsync(
        string conversationId,
        AiTaskPlanGenerateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("规划内容不能为空", ErrorCodes.ParameterInvalid);
        }

        var endpoint = await modelRouter.ResolveAsync(request.ModelConfigId, cancellationToken);
        var output = await chatRuntime.CompleteAsync(new AiKernelChatRequest
        {
            Endpoint = endpoint,
            JsonResponse = true,
            Messages =
            [
                new ChatMessageContent(AuthorRole.System, parser.BuildPlanPrompt()),
                new ChatMessageContent(AuthorRole.User, request.Content.Trim())
            ]
        }, cancellationToken);

        return await taskPlanService.CreateAsync(conversationId, parser.Parse(output), null, cancellationToken);
    }
}
