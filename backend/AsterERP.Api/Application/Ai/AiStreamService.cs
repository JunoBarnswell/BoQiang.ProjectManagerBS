using System.Diagnostics;
using System.Text.Json;
using AsterERP.Api.Application.Ai.Agent;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiStreamService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    AiConversationService conversationService,
    AiGovernanceService governanceService,
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    AiContextBuilder contextBuilder,
    AiRunConcurrencyGuard concurrencyGuard,
    IAiTaskPlanService taskPlanService,
    IAiAgentExecutionService agentExecutionService,
    AiPlanParser planParser,
    AiRunCancellationRegistry cancellationRegistry,
    SseEventWriter sseWriter,
    ILogger<AiStreamService> logger) : IAiStreamService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task StreamAsync(
        string conversationId,
        AiChatStreamRequest request,
        HttpResponse response,
        string traceId,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaceContext.Resolve();
        var settings = await governanceService.GetSecuritySettingsAsync(cancellationToken);
        ValidateStreamRequest(request, settings);

        AiConversationEntity conversation;
        AiChatRunEntity run;
        AiMessageEntity userMessage;
        AiModelEndpoint endpoint;
        await using (await concurrencyGuard.AcquireConversationAsync(conversationId, cancellationToken))
        {
            conversation = await conversationService.RequireConversationAsync(conversationId, cancellationToken);
            await concurrencyGuard.EnsureNoActiveRunAsync(conversationId, cancellationToken);
            endpoint = await modelRouter.ResolveAsync(request.ModelConfigId, cancellationToken);
            var nextSeq = await NextSeqAsync(conversationId, cancellationToken);
            userMessage = new AiMessageEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                ConversationId = conversationId,
                Role = "user",
                Seq = nextSeq,
                Content = request.Content.Trim(),
                Status = "Completed",
                TokenCount = EstimateTokens(request.Content)
            };
            await db.Insertable(userMessage).ExecuteCommandAsync(cancellationToken);

            run = new AiChatRunEntity
            {
                TenantId = workspace.TenantId,
                AppCode = workspace.AppCode,
                OwnerUserId = workspace.UserId,
                ConversationId = conversationId,
                UserMessageId = userMessage.Id,
                ProviderId = endpoint.ProviderId,
                ModelConfigId = endpoint.ModelConfigId,
                Mode = NormalizeMode(request.Mode),
                Status = "Running",
                ClientMessageId = NormalizeOptional(request.ClientMessageId),
                IdempotencyKey = NormalizeOptional(request.IdempotencyKey),
                RequestHash = request.Content.GetHashCode(StringComparison.Ordinal).ToString(),
                AgentProfileIdsJson = JsonSerializer.Serialize(request.AgentProfileIds, JsonOptions),
                CoordinatorAgentProfileId = NormalizeOptional(request.CoordinatorAgentProfileId),
                TraceId = traceId,
                StartedAt = DateTime.UtcNow
            };
            await db.Insertable(run).ExecuteCommandAsync(cancellationToken);

            userMessage.RunId = run.Id;
            await db.Updateable(userMessage).ExecuteCommandAsync(cancellationToken);
            conversation.LastRunStatus = "Running";
            conversation.LastMessageAt = DateTime.UtcNow;
            await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
        }

        var streamSeq = 0L;
        var stopwatch = Stopwatch.StartNew();
        using var linkedCancellation = cancellationRegistry.CreateLinked(run.Id, cancellationToken);
        AiMessageEntity? assistantForFailure = null;
        try
        {
            await EmitAsync(response, "run_started", run.Id, conversationId, traceId, ++streamSeq, new { run.Id, run.Mode }, cancellationToken);
            await EmitAsync(response, "context_built", run.Id, conversationId, traceId, ++streamSeq, new { endpoint.ModelCode, endpoint.ProviderCode }, cancellationToken);

            var workMode = NormalizeWorkMode(request.WorkMode);

            var assistant = workMode == "Agent"
                ? await RunTaskPlanAgentAsync(conversation, run, request, streamSeq, linkedCancellation.Token)
                : run.Mode == "Collaborative"
                ? await StreamCollaborativeAsync(conversation, run, request, endpoint, response, traceId, streamSeq, linkedCancellation.Token)
                : await StreamSingleAsync(conversation, run, request, endpoint, response, traceId, streamSeq, linkedCancellation.Token);

            streamSeq = assistant.StreamSeq;
            assistantForFailure = assistant.Message;
            AiTaskPlanDto? taskPlan = null;
            var completionStatus = "Succeeded";
            if (workMode == "Plan")
            {
                taskPlan = await taskPlanService.CreateFromAssistantContentAsync(conversation, run.Id, assistant.Message.Content, cancellationToken);
                completionStatus = "PlanReady";
            }

            stopwatch.Stop();
            await CompleteRunAsync(conversation, run, assistant.Message, assistant.Usage, endpoint, stopwatch.ElapsedMilliseconds, completionStatus, cancellationToken);
            await EmitAsync(response, "usage", run.Id, conversationId, traceId, ++streamSeq, assistant.Usage, cancellationToken);
            await EmitAsync(response, "done", run.Id, conversationId, traceId, ++streamSeq, new { status = completionStatus, taskPlanId = taskPlan?.Id }, cancellationToken);
        }
        catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
        {
            stopwatch.Stop();
            await MarkRunFailedAsync(run, "Cancelled", ErrorCodes.AiStreamInterrupted, "用户已停止生成", endpoint, stopwatch.ElapsedMilliseconds, cancellationToken);
            await EmitAsync(response, "error", run.Id, conversationId, traceId, ++streamSeq, new { code = ErrorCodes.AiStreamInterrupted, message = "用户已停止生成" }, CancellationToken.None);
            await EmitAsync(response, "done", run.Id, conversationId, traceId, ++streamSeq, new { status = "Cancelled" }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            logger.LogError(ex, "AI stream failed for run {RunId}", run.Id);
            if (assistantForFailure is not null && ex is ValidationException validationException && validationException.Code == ErrorCodes.AiPlanParseFailed)
            {
                await PersistAssistantOnFailureAsync(conversation, run, assistantForFailure, CancellationToken.None);
                await MarkRunFailedAsync(run, "PlanParseFailed", validationException.Code, ex.Message, endpoint, stopwatch.ElapsedMilliseconds, CancellationToken.None);
                if (!response.HasStarted)
                {
                    throw;
                }

                await EmitAsync(response, "done", run.Id, conversationId, traceId, ++streamSeq, new { status = "PlanParseFailed" }, CancellationToken.None);
                return;
            }

            var errorCode = ResolveErrorCode(ex);
            await MarkRunFailedAsync(run, "Failed", errorCode, ex.Message, endpoint, stopwatch.ElapsedMilliseconds, CancellationToken.None);
            if (!response.HasStarted)
            {
                throw;
            }

            await EmitAsync(response, "error", run.Id, conversationId, traceId, ++streamSeq, new { code = errorCode, message = ex.Message }, CancellationToken.None);
            await EmitAsync(response, "done", run.Id, conversationId, traceId, ++streamSeq, new { status = "Failed" }, CancellationToken.None);
        }
        finally
        {
            cancellationRegistry.Complete(run.Id);
        }
    }

    public async Task<bool> StopRunAsync(string runId, CancellationToken cancellationToken = default)
    {
        var run = await db.Queryable<AiChatRunEntity>()
            .FirstAsync(item => item.Id == runId && !item.IsDeleted, cancellationToken)
            ?? throw new NotFoundException("运行记录不存在", ErrorCodes.AiRunNotFound);

        if (run.Status is not ("Queued" or "Running"))
        {
            return false;
        }

        run.Status = "Cancelling";
        run.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(run).ExecuteCommandAsync(cancellationToken);
        return cancellationRegistry.Cancel(runId);
    }

    private async Task<AssistantStreamResult> StreamSingleAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiChatStreamRequest request,
        AiModelEndpoint endpoint,
        HttpResponse response,
        string traceId,
        long streamSeq,
        CancellationToken cancellationToken)
    {
        var messages = await BuildMessagesForWorkModeAsync(conversation, request, null, cancellationToken);
        var kernelRequest = BuildKernelRequest(endpoint, request, messages, conversation.OwnerUserId);
        var result = await StreamKernelToClientAsync(kernelRequest, run.Id, conversation.Id, response, traceId, streamSeq, null, cancellationToken);
        var nextSeq = await NextSeqAsync(conversation.Id, cancellationToken);
        var assistant = new AiMessageEntity
        {
            TenantId = conversation.TenantId,
            AppCode = conversation.AppCode,
            OwnerUserId = conversation.OwnerUserId,
            ConversationId = conversation.Id,
            RunId = run.Id,
            ParentMessageId = run.UserMessageId,
            Role = "assistant",
            Seq = nextSeq,
            Content = result.Content,
            TokenCount = result.Usage.TotalTokens,
            FinishReason = result.FinishReason,
            Status = "Completed"
        };
        return new AssistantStreamResult(assistant, result.Usage, result.StreamSeq);
    }

    private async Task<AssistantStreamResult> RunTaskPlanAgentAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiChatStreamRequest request,
        long streamSeq,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TaskPlanId))
        {
            throw new ValidationException("Agent 模式需要选择已批准的任务计划", ErrorCodes.AiTaskPlanNotFound);
        }

        var seq = streamSeq;
        var execution = await agentExecutionService.ExecuteAsync(
            request.TaskPlanId,
            run.Id,
            request.ModelConfigId,
            request.Content,
            request.EnabledToolCodes,
            request.EnabledToolDomains,
            null,
            cancellationToken);

        var content = execution.Summary;
        var nextSeq = await NextSeqAsync(conversation.Id, cancellationToken);
        var assistant = new AiMessageEntity
        {
            TenantId = conversation.TenantId,
            AppCode = conversation.AppCode,
            OwnerUserId = conversation.OwnerUserId,
            ConversationId = conversation.Id,
            RunId = run.Id,
            ParentMessageId = run.UserMessageId,
            Role = "assistant",
            Seq = nextSeq,
            Content = content,
            TokenCount = EstimateTokens(content),
            FinishReason = "task_plan_executed",
            Status = "Completed"
        };
        var usage = new AiChatUsage { CompletionTokens = assistant.TokenCount, TotalTokens = assistant.TokenCount };
        return new AssistantStreamResult(assistant, usage, seq);
    }

    private async Task<AssistantStreamResult> StreamCollaborativeAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiChatStreamRequest request,
        AiModelEndpoint endpoint,
        HttpResponse response,
        string traceId,
        long streamSeq,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        throw new BusinessException(
            ErrorCodes.AiFrameworkCapabilityUnavailable,
            "协作模式必须迁移到 SK AgentGroupChat selection/termination strategy；旧手写并行协调链路已删除，当前入口阻断。");
    }

    private async Task<KernelAggregate> StreamKernelToClientAsync(
        AiKernelChatRequest kernelRequest,
        string runId,
        string conversationId,
        HttpResponse response,
        string traceId,
        long streamSeq,
        string? agentId,
        CancellationToken cancellationToken)
    {
        var aggregate = new KernelAggregate { StreamSeq = streamSeq };
        var contentStarted = false;
        try
        {
            await foreach (var chunk in chatRuntime.StreamAsync(kernelRequest, cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.ContentDelta))
                {
                    if (!contentStarted)
                    {
                        await EmitAsync(response, "content_started", runId, conversationId, traceId, ++aggregate.StreamSeq, new { agentId }, cancellationToken);
                        contentStarted = true;
                    }

                    aggregate.Content += chunk.ContentDelta;
                    await EmitAsync(response, "content_delta", runId, conversationId, traceId, ++aggregate.StreamSeq, new { agentId, delta = chunk.ContentDelta }, cancellationToken);
                }

                if (chunk.Usage is not null)
                {
                    aggregate.Usage = chunk.Usage;
                }

                if (!string.IsNullOrWhiteSpace(chunk.FinishReason))
                {
                    aggregate.FinishReason = chunk.FinishReason;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException(ErrorCodes.AiModelServiceUnavailable, $"模型服务不可用：{ex.Message}");
        }

        await EmitAsync(response, "content_completed", runId, conversationId, traceId, ++aggregate.StreamSeq, new { agentId }, cancellationToken);
        EnsureUsageFallback(aggregate);
        return aggregate;
    }

    private async Task<KernelAggregate> StreamKernelToBufferAsync(AiKernelChatRequest kernelRequest, CancellationToken cancellationToken)
    {
        var aggregate = new KernelAggregate();
        try
        {
            await foreach (var chunk in chatRuntime.StreamAsync(kernelRequest, cancellationToken))
            {
                aggregate.Content += chunk.ContentDelta;
                aggregate.FinishReason = chunk.FinishReason ?? aggregate.FinishReason;
                aggregate.Usage = chunk.Usage ?? aggregate.Usage;
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BusinessException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new BusinessException(ErrorCodes.AiModelServiceUnavailable, $"模型服务不可用：{ex.Message}");
        }

        EnsureUsageFallback(aggregate);
        return aggregate;
    }

    private async Task CompleteRunAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiMessageEntity assistant,
        AiChatUsage usage,
        AiModelEndpoint endpoint,
        long durationMs,
        string completionStatus,
        CancellationToken cancellationToken)
    {
        await using (await concurrencyGuard.AcquireConversationAsync(conversation.Id, cancellationToken))
        {
            await db.Insertable(assistant).ExecuteCommandAsync(cancellationToken);
            run.AssistantMessageId = assistant.Id;
            run.Status = completionStatus;
            run.PromptTokens = usage.PromptTokens;
            run.CompletionTokens = usage.CompletionTokens;
            run.ReasoningTokens = usage.ReasoningTokens;
            run.TotalTokens = usage.TotalTokens;
            run.CompletedAt = DateTime.UtcNow;
            run.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(run).ExecuteCommandAsync(cancellationToken);

            conversation.LastRunStatus = completionStatus;
            conversation.LastMessageAt = DateTime.UtcNow;
            if (conversation.Title == "新会话")
            {
                conversation.Title = assistant.Content.Length > 24 ? $"{assistant.Content[..24]}..." : assistant.Content;
            }

            await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
            await WriteUsageAsync(conversation, run, endpoint, usage, true, null, durationMs, cancellationToken);
        }
    }

    private async Task MarkRunFailedAsync(AiChatRunEntity run, string status, int errorCode, string message, AiModelEndpoint endpoint, long durationMs, CancellationToken cancellationToken)
    {
        run.Status = status;
        run.ErrorCode = errorCode.ToString();
        run.ErrorMessage = message;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(run).ExecuteCommandAsync(cancellationToken);
        var conversation = await db.Queryable<AiConversationEntity>().FirstAsync(item => item.Id == run.ConversationId, cancellationToken);
        if (conversation is not null)
        {
            conversation.LastRunStatus = status;
            conversation.UpdatedTime = DateTime.UtcNow;
            await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
            await WriteUsageAsync(conversation, run, endpoint, new AiChatUsage(), false, message, durationMs, cancellationToken);
        }
    }

    private static int ResolveErrorCode(Exception exception) =>
        exception is BusinessException businessException ? businessException.Code : ErrorCodes.AiStreamInterrupted;

    private async Task WriteUsageAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiModelEndpoint endpoint,
        AiChatUsage usage,
        bool isSuccess,
        string? errorMessage,
        long durationMs,
        CancellationToken cancellationToken)
    {
        var log = new AiUsageLogEntity
        {
            TenantId = conversation.TenantId,
            AppCode = conversation.AppCode,
            UserId = conversation.OwnerUserId,
            ConversationId = conversation.Id,
            RunId = run.Id,
            ProviderCode = endpoint.ProviderCode,
            ModelCode = endpoint.ModelCode,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            ReasoningTokens = usage.ReasoningTokens,
            TotalTokens = usage.TotalTokens,
            DurationMs = (int)Math.Min(int.MaxValue, durationMs),
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            RequestStartedAt = run.StartedAt ?? run.CreatedTime,
            RequestCompletedAt = DateTime.UtcNow
        };
        await db.Insertable(log).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<AiAgentProfileEntity>> ResolveAgentsAsync(
        AiChatStreamRequest request,
        AiSecuritySettingsDto settings,
        CancellationToken cancellationToken)
    {
        var ids = request.AgentProfileIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).Take(settings.MaxParallelAgents).ToArray();
        if (ids.Length == 0)
        {
            throw new ValidationException("协作模式至少需要选择一个智能体", ErrorCodes.ParameterInvalid);
        }

        var agents = await db.Queryable<AiAgentProfileEntity>()
            .Where(item => !item.IsDeleted && item.IsEnabled && ids.Contains(item.Id))
            .OrderBy(item => item.SortOrder)
            .ToListAsync(cancellationToken);
        if (agents.Count == 0)
        {
            throw new ValidationException("未找到可用智能体", ErrorCodes.AiAgentProfileNotFound);
        }

        return agents;
    }

    private static AiKernelChatRequest BuildKernelRequest(
        AiModelEndpoint endpoint,
        AiChatStreamRequest request,
        IReadOnlyList<ChatMessageContent> messages,
        string ownerUserId,
        string? agentName = null)
    {
        var workMode = NormalizeWorkMode(request.WorkMode);
        var isPlanMode = workMode == "Plan";
        var extraParameters = BuildExtraParametersForWorkMode(request);
        return new AiKernelChatRequest
        {
            Endpoint = endpoint,
            Messages = messages,
            JsonResponse = isPlanMode,
            UserId = ownerUserId,
            AgentName = agentName,
            EnabledFunctionNames = request.EnabledToolCodes,
            ExtraParameters = extraParameters
        };
    }

    private static Dictionary<string, object?> BuildExtraParametersForWorkMode(AiChatStreamRequest request)
    {
        var extraParameters = new Dictionary<string, object?>(request.ExtraParameters, StringComparer.OrdinalIgnoreCase);
        if (NormalizeWorkMode(request.WorkMode) == "Plan")
        {
            extraParameters.Remove("response_format");
        }

        return extraParameters;
    }

    private async Task<int> NextSeqAsync(string conversationId, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .OrderBy(item => item.Seq, OrderByType.Desc)
            .Take(1)
            .ToListAsync(cancellationToken);
        return (rows.FirstOrDefault()?.Seq ?? 0) + 1;
    }

    private Task EmitAsync(HttpResponse response, string eventName, string runId, string conversationId, string traceId, long seq, object? data, CancellationToken cancellationToken) =>
        sseWriter.WriteAsync(response, new AiStreamEventDto
        {
            Event = eventName,
            RunId = runId,
            ConversationId = conversationId,
            TraceId = traceId,
            Seq = seq,
            Timestamp = DateTime.UtcNow,
            Data = data
        }, cancellationToken);

    private static void ValidateStreamRequest(AiChatStreamRequest request, AiSecuritySettingsDto settings)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("消息内容不能为空", ErrorCodes.ParameterInvalid);
        }

        if (request.Content.Length > settings.MaxInputCharacters)
        {
            throw new ValidationException($"消息内容不能超过 {settings.MaxInputCharacters} 个字符", ErrorCodes.AiContextTooLarge);
        }
    }

    private static string NormalizeMode(string? mode) =>
        string.Equals(mode, "Collaborative", StringComparison.OrdinalIgnoreCase) ? "Collaborative" : "Single";

    private static string NormalizeWorkMode(string? mode)
    {
        if (string.Equals(mode, "Plan", StringComparison.OrdinalIgnoreCase))
        {
            return "Plan";
        }

        return string.Equals(mode, "Agent", StringComparison.OrdinalIgnoreCase) ? "Agent" : "Ask";
    }

    private async Task<IReadOnlyList<ChatMessageContent>> BuildMessagesForWorkModeAsync(
        AiConversationEntity conversation,
        AiChatStreamRequest request,
        string? agentPrompt,
        CancellationToken cancellationToken)
    {
        var messages = (await contextBuilder.BuildAsync(conversation, request, agentPrompt, cancellationToken)).ToList();
        if (NormalizeWorkMode(request.WorkMode) != "Plan")
        {
            return messages;
        }

        var planPrompt = planParser.BuildPlanPrompt();
        if (messages.Count > 0 && messages[0].Role == AuthorRole.System)
        {
            messages[0] = new ChatMessageContent(AuthorRole.System, $"{messages[0].Content}\n\n{planPrompt}");
            return messages;
        }

        messages.Insert(0, new ChatMessageContent(AuthorRole.System, planPrompt));
        return messages;
    }

    private async Task PersistAssistantOnFailureAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiMessageEntity assistant,
        CancellationToken cancellationToken)
    {
        await using (await concurrencyGuard.AcquireConversationAsync(conversation.Id, cancellationToken))
        {
            var exists = await db.Queryable<AiMessageEntity>()
                .AnyAsync(item => item.Id == assistant.Id && !item.IsDeleted, cancellationToken);
            if (exists)
            {
                return;
            }

            assistant.Status = "Failed";
            await db.Insertable(assistant).ExecuteCommandAsync(cancellationToken);
            run.AssistantMessageId = assistant.Id;
            await db.Updateable(run).ExecuteCommandAsync(cancellationToken);
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static int EstimateTokens(string? content) => string.IsNullOrEmpty(content) ? 0 : Math.Max(1, content.Length / 4);

    private static void EnsureUsageFallback(KernelAggregate aggregate)
    {
        if (aggregate.Usage.TotalTokens > 0)
        {
            return;
        }

        aggregate.Usage.CompletionTokens = EstimateTokens(aggregate.Content);
        aggregate.Usage.TotalTokens = aggregate.Usage.CompletionTokens;
    }

    private sealed class KernelAggregate
    {
        public string Content { get; set; } = string.Empty;

        public string? FinishReason { get; set; }

        public AiChatUsage Usage { get; set; } = new();

        public long StreamSeq { get; set; }
    }

    private sealed record AssistantStreamResult(AiMessageEntity Message, AiChatUsage Usage, long StreamSeq);

    private sealed record AgentResult(AiAgentProfileEntity Agent, KernelAggregate? Aggregate, string? ErrorMessage);
}
