using System.Collections.Concurrent;
using System.Text.Json;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowisePredictionService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IFlowiseExecutionService executionService,
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    FlowisePermissionGuard permissionGuard) : IFlowisePredictionService
{
    private const int DefaultPageSize = 20;
    private const int MaxFileUploadCount = 10;
    private const int MaxFileUploadDataLength = 5 * 1024 * 1024;
    private const int MaxPageSize = 500;
    private const string DefaultFollowUpPrompt = "Given the following conversations: {history}. Please help me predict the three most likely questions that human would ask and keeping each question short and concise.";
    private static readonly ConcurrentDictionary<string, CancellationTokenSource> ActiveStreamAbortControllers = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GridPageResult<FlowiseChatMessageDto>> GetMessagesAsync(FlowisePredictionListQuery query, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(query.ResourceId, cancellationToken);
        EnsureView(chatflow.Type);
        var dbQuery = db.Queryable<FlowiseChatMessageEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id);
        if (!string.IsNullOrWhiteSpace(query.ChatId))
        {
            var chatId = query.ChatId.Trim();
            dbQuery = dbQuery.Where(item => item.ChatId == chatId);
        }

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePageIndex(query.PageIndex), NormalizePageSize(query.PageSize), total);
        var feedbackMap = await LoadLatestFeedbackMapAsync(rows.Select(item => item.Id), cancellationToken);
        return new GridPageResult<FlowiseChatMessageDto>
        {
            Total = total.Value,
            Items = rows.Select(item => MapMessage(item, feedbackMap)).ToList()
        };
    }

    public async Task<GridPageResult<FlowiseLeadDto>> GetLeadsAsync(FlowisePredictionListQuery query, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(query.ResourceId, cancellationToken);
        EnsureView(chatflow.Type);
        var dbQuery = db.Queryable<FlowiseLeadEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id);

        var total = new RefAsync<int>();
        var rows = await dbQuery.OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToPageListAsync(NormalizePageIndex(query.PageIndex), NormalizePageSize(query.PageSize), total);
        return new GridPageResult<FlowiseLeadDto>
        {
            Total = total.Value,
            Items = rows.Select(MapLead).ToList()
        };
    }

    public async Task<FlowisePredictionResponse> PredictAsync(FlowisePredictionRequest request, CancellationToken cancellationToken)
    {
        var question = NormalizePredictionQuestion(request);
        var hasForm = HasFormInput(request);
        if (string.IsNullOrWhiteSpace(request.ResourceId) || (string.IsNullOrWhiteSpace(question) && !hasForm && (request.Uploads is null || request.Uploads.Count == 0)))
        {
            throw new ValidationException("Flowise prediction 缺少资源或问题", ErrorCodes.ParameterInvalid);
        }

        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureRun(chatflow.Type);
        var uploads = NormalizeUploads(request.Uploads);
        var workspace = workspaceContext.Resolve();
        var chatId = NormalizeOptional(request.ChatId) ?? Guid.NewGuid().ToString("N");
        var chatHistory = await LoadChatHistoryAsync(chatflow.Id, chatId, cancellationToken);
        var humanInput = await ResolveHumanInputResumeAsync(chatflow.Id, chatId, request, cancellationToken);
        var userMessage = new FlowiseChatMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ChatId = chatId,
            Role = "user",
            Message = question,
            FileUploadsJson = JsonSerializer.Serialize(uploads)
        };
        await db.Insertable(userMessage).ExecuteCommandAsync(cancellationToken);

        var execution = await executionService.StartAsync(new FlowiseExecutionStartRequest
        {
            IdempotencyKey = $"{chatflow.Id}:{userMessage.Id}",
            InputJson = JsonSerializer.Serialize(new { question, form = request.Form, humanInput, chatId = userMessage.ChatId, request.SessionId, chatHistory, uploads }),
            ChatId = userMessage.ChatId,
            ChatHistory = chatHistory,
            Form = request.Form,
            HumanInput = humanInput,
            Question = question,
            ResourceId = chatflow.Id,
            SessionId = request.SessionId,
            Webhook = null
        }, cancellationToken);
        var assistantAnswer = execution.Status == "Failed"
            ? execution.ErrorMessage ?? "Flowise execution failed."
            : ExtractResponseText(execution.OutputJson);
        var followUpPromptsJson = await GenerateFollowUpPromptsJsonAsync(chatflow, assistantAnswer, cancellationToken);
        var assistantMessage = new FlowiseChatMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ExecutionId = execution.Id,
            ChatId = userMessage.ChatId,
            Role = "assistant",
            Message = assistantAnswer,
            SourceDocumentsJson = execution.SourceDocumentsJson,
            AgentReasoningJson = ExtractJsonArray(execution.OutputJson, "agentReasoning", "AgentReasoning"),
            AgentExecutedDataJson = ExtractJsonArray(execution.OutputJson, "agentExecutedData", "AgentExecutedData"),
            UsedToolsJson = ExtractJsonArray(execution.OutputJson, "usedTools", "UsedTools"),
            ArtifactsJson = ExtractJsonArray(execution.OutputJson, "artifacts", "Artifacts"),
            ActionJson = execution.ActionJson,
            FollowUpPromptsJson = followUpPromptsJson
        };
        await db.Insertable(assistantMessage).ExecuteCommandAsync(cancellationToken);

        return new FlowisePredictionResponse
        {
            Execution = execution,
            Message = MapMessage(assistantMessage, new Dictionary<string, FlowiseFeedbackDto>())
        };
    }

    public async Task StreamAsync(FlowisePredictionRequest request, HttpResponse response, CancellationToken cancellationToken)
    {
        var question = NormalizePredictionQuestion(request);
        var hasForm = HasFormInput(request);
        if (string.IsNullOrWhiteSpace(request.ResourceId) || (string.IsNullOrWhiteSpace(question) && !hasForm && (request.Uploads is null || request.Uploads.Count == 0)))
        {
            throw new ValidationException("Flowise prediction 缺少资源或问题", ErrorCodes.ParameterInvalid);
        }

        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureRun(chatflow.Type);
        var uploads = NormalizeUploads(request.Uploads);
        var workspace = workspaceContext.Resolve();
        var chatId = NormalizeOptional(request.ChatId) ?? Guid.NewGuid().ToString("N");
        var chatHistory = await LoadChatHistoryAsync(chatflow.Id, chatId, cancellationToken);
        var humanInput = await ResolveHumanInputResumeAsync(chatflow.Id, chatId, request, cancellationToken);
        var userMessage = new FlowiseChatMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ChatId = chatId,
            Role = "user",
            Message = question,
            FileUploadsJson = JsonSerializer.Serialize(uploads)
        };
        await db.Insertable(userMessage).ExecuteCommandAsync(cancellationToken);

        response.ContentType = "text/event-stream";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        await response.StartAsync(cancellationToken);

        object? executionEndPayload = null;
        var abortKey = CreateAbortKey(chatflow.Id, chatId);
        using var streamAbortController = RegisterStreamAbortController(abortKey);
        using var streamCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, streamAbortController.Token);
        var streamToken = streamCancellation.Token;
        FlowiseExecutionDto execution;
        try
        {
            execution = await executionService.StreamAsync(new FlowiseExecutionStartRequest
            {
                IdempotencyKey = $"{chatflow.Id}:{userMessage.Id}:stream",
                InputJson = JsonSerializer.Serialize(new { question, form = request.Form, humanInput, chatId = userMessage.ChatId, request.SessionId, chatHistory, uploads }),
                ChatId = userMessage.ChatId,
                ChatHistory = chatHistory,
                Form = request.Form,
                HumanInput = humanInput,
                Question = question,
                ResourceId = chatflow.Id,
                SessionId = request.SessionId,
                Webhook = null
            }, async (eventName, data, token) =>
            {
                if (string.Equals(eventName, "end", StringComparison.Ordinal))
                {
                    executionEndPayload = data;
                    return;
                }

                await WriteSseEventAsync(response, eventName, data, token);
            }, streamToken);
        }
        finally
        {
            UnregisterStreamAbortController(abortKey, streamAbortController);
        }
        if (cancellationToken.IsCancellationRequested || execution.Status == "Cancelled")
        {
            return;
        }

        var assistantAnswer = execution.Status == "Failed" || execution.Status == "Cancelled"
            ? execution.ErrorMessage ?? "Flowise execution failed."
            : ExtractResponseText(execution.OutputJson);
        var followUpPromptsJson = await GenerateFollowUpPromptsJsonAsync(chatflow, assistantAnswer, CancellationToken.None);
        var assistantMessage = new FlowiseChatMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ExecutionId = execution.Id,
            ChatId = userMessage.ChatId,
            Role = "assistant",
            Message = assistantAnswer,
            SourceDocumentsJson = execution.SourceDocumentsJson,
            AgentReasoningJson = ExtractJsonArray(execution.OutputJson, "agentReasoning", "AgentReasoning"),
            AgentExecutedDataJson = ExtractJsonArray(execution.OutputJson, "agentExecutedData", "AgentExecutedData"),
            UsedToolsJson = ExtractJsonArray(execution.OutputJson, "usedTools", "UsedTools"),
            ArtifactsJson = ExtractJsonArray(execution.OutputJson, "artifacts", "Artifacts"),
            ActionJson = execution.ActionJson,
            FollowUpPromptsJson = followUpPromptsJson
        };
        await db.Insertable(assistantMessage).ExecuteCommandAsync(CancellationToken.None);
        await WriteSseEventAsync(response, "metadata", new
        {
            chatId = assistantMessage.ChatId,
            executionId = execution.Id,
            message = MapMessage(assistantMessage, new Dictionary<string, FlowiseFeedbackDto>()),
            status = execution.Status,
            traceId = execution.TraceId
        }, CancellationToken.None);
        await WriteSseEventAsync(response, "end", executionEndPayload ?? execution, CancellationToken.None);
    }

    public async Task<FlowiseFeedbackDto> SaveFeedbackAsync(FlowiseFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.MessageId) || string.IsNullOrWhiteSpace(request.Rating))
        {
            throw new ValidationException("反馈缺少消息或评分", ErrorCodes.ParameterInvalid);
        }

        var message = await db.Queryable<FlowiseChatMessageEntity>()
            .FirstAsync(item => !item.IsDeleted && item.Id == request.MessageId.Trim(), cancellationToken)
            ?? throw new ValidationException("Flowise 消息不存在", ErrorCodes.ParameterInvalid);
        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseFeedbackEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = message.WorkspaceId,
            MessageId = message.Id,
            Rating = request.Rating.Trim(),
            Reason = NormalizeOptional(request.Reason)
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return new FlowiseFeedbackDto
        {
            Id = entity.Id,
            MessageId = entity.MessageId,
            Rating = entity.Rating,
            Reason = entity.Reason
        };
    }

    public async Task<FlowiseLeadDto> SaveLeadAsync(FlowiseLeadRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureView(chatflow.Type);
        var workspace = workspaceContext.Resolve();
        var entity = new FlowiseLeadEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            WorkspaceId = chatflow.WorkspaceId,
            ResourceId = chatflow.Id,
            ContactJson = NormalizeJson(request.ContactJson)
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return MapLead(entity);
    }

    public async Task<bool> ClearChatAsync(FlowiseChatClearRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureView(chatflow.Type);
        var chatId = NormalizeOptional(request.ChatId);
        var messages = await db.Queryable<FlowiseChatMessageEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id)
            .WhereIF(!string.IsNullOrWhiteSpace(chatId), item => item.ChatId == chatId)
            .ToListAsync(cancellationToken);
        if (messages.Count == 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        foreach (var item in messages)
        {
            item.IsDeleted = true;
            item.DeletedTime = now;
        }

        await db.Updateable(messages).ExecuteCommandAsync(cancellationToken);
        var messageIds = messages.Select(item => item.Id).ToList();
        var feedbackRows = await db.Queryable<FlowiseFeedbackEntity>()
            .Where(item => !item.IsDeleted && messageIds.Contains(item.MessageId))
            .ToListAsync(cancellationToken);
        foreach (var item in feedbackRows)
        {
            item.IsDeleted = true;
            item.DeletedTime = now;
        }

        if (feedbackRows.Count > 0)
        {
            await db.Updateable(feedbackRows).ExecuteCommandAsync(cancellationToken);
        }

        var leadRows = await db.Queryable<FlowiseLeadEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id)
            .ToListAsync(cancellationToken);
        var deletedLeads = leadRows.Where(item => string.IsNullOrWhiteSpace(chatId) || ContactMatchesChat(item.ContactJson, chatId)).ToList();
        foreach (var item in deletedLeads)
        {
            item.IsDeleted = true;
            item.DeletedTime = now;
        }

        if (deletedLeads.Count > 0)
        {
            await db.Updateable(deletedLeads).ExecuteCommandAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> AbortChatAsync(FlowisePredictionAbortRequest request, CancellationToken cancellationToken)
    {
        var chatflow = await LoadChatFlowAsync(request.ResourceId, cancellationToken);
        EnsureRun(chatflow.Type);
        var chatId = NormalizeOptional(request.ChatId);
        if (string.IsNullOrWhiteSpace(chatId))
        {
            return true;
        }

        CancelActiveStream(CreateAbortKey(chatflow.Id, chatId));

        var userMessages = await db.Queryable<FlowiseChatMessageEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id && item.ChatId == chatId && item.Role == "user")
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        if (userMessages.Count == 0)
        {
            return true;
        }

        var executionKeys = userMessages
            .Select(item => $"{chatflow.Id}:{item.Id}:stream")
            .Concat(userMessages.Select(item => $"{chatflow.Id}:{item.Id}"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var executions = await db.Queryable<FlowiseExecutionEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == chatflow.Id && item.IdempotencyKey != null && executionKeys.Contains(item.IdempotencyKey))
            .Where(item => item.Status == "Running" || item.Status == "Queued")
            .ToListAsync(cancellationToken);
        if (executions.Count == 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        foreach (var execution in executions)
        {
            execution.Status = "Cancelled";
            execution.ErrorCode = "FLOWISE_CHAT_ABORTED";
            execution.ErrorMessage = "Flowise chat run aborted.";
            execution.CompletedAt = now;
            execution.UpdatedTime = now;
        }

        await db.Updateable(executions).ExecuteCommandAsync(cancellationToken);
        return true;
    }

    private static string CreateAbortKey(string resourceId, string chatId) => $"{resourceId.Trim()}_{chatId.Trim()}";

    private static CancellationTokenSource RegisterStreamAbortController(string abortKey)
    {
        var controller = new CancellationTokenSource();
        var existing = ActiveStreamAbortControllers.AddOrUpdate(abortKey, controller, (_, previous) =>
        {
            CancelAndDispose(previous);
            return controller;
        });

        return existing;
    }

    private static void UnregisterStreamAbortController(string abortKey, CancellationTokenSource controller)
    {
        if (ActiveStreamAbortControllers.TryRemove(new KeyValuePair<string, CancellationTokenSource>(abortKey, controller)))
        {
            controller.Dispose();
        }
    }

    private static void CancelActiveStream(string abortKey)
    {
        if (ActiveStreamAbortControllers.TryGetValue(abortKey, out var controller))
        {
            controller.Cancel();
        }
    }

    private static void CancelAndDispose(CancellationTokenSource controller)
    {
        try
        {
            controller.Cancel();
        }
        finally
        {
            controller.Dispose();
        }
    }

    private async Task<FlowiseChatFlowEntity> LoadChatFlowAsync(string resourceId, CancellationToken cancellationToken) =>
        await db.Queryable<FlowiseChatFlowEntity>().FirstAsync(item => !item.IsDeleted && item.Id == resourceId.Trim(), cancellationToken)
        ?? throw new ValidationException("Flowise ChatFlow 不存在", ErrorCodes.ParameterInvalid);

    private void EnsureView(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsView, PermissionCodes.FlowiseView, PermissionCodes.FlowiseManage);
    }

    private void EnsureRun(string type)
    {
        if (type.Equals(FlowiseChatflowTypes.Agentflow, StringComparison.OrdinalIgnoreCase))
        {
            permissionGuard.EnsureAny(PermissionCodes.FlowiseAgentflowsRun, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
            return;
        }

        permissionGuard.EnsureAny(PermissionCodes.FlowiseChatflowsRun, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
    }

    private async Task<IReadOnlyDictionary<string, FlowiseFeedbackDto>> LoadLatestFeedbackMapAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken)
    {
        var ids = messageIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<string, FlowiseFeedbackDto>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = await db.Queryable<FlowiseFeedbackEntity>()
            .Where(item => !item.IsDeleted && ids.Contains(item.MessageId))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .ToListAsync(cancellationToken);
        return rows.GroupBy(item => item.MessageId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => MapFeedback(group.First()), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<FlowiseChatHistoryMessageDto>> LoadChatHistoryAsync(string resourceId, string chatId, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<FlowiseChatMessageEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == resourceId && item.ChatId == chatId)
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return rows
            .OrderBy(item => item.CreatedTime)
            .Select(item => new FlowiseChatHistoryMessageDto
            {
                Content = item.Message,
                CreatedTime = item.CreatedTime,
                Role = item.Role
            })
            .ToList();
    }

    private static FlowiseChatMessageDto MapMessage(FlowiseChatMessageEntity entity, IReadOnlyDictionary<string, FlowiseFeedbackDto> feedbackMap) => new()
    {
        ChatId = entity.ChatId,
        CreatedTime = entity.CreatedTime,
        ExecutionId = entity.ExecutionId,
        Feedback = feedbackMap.GetValueOrDefault(entity.Id),
        FileUploads = ParseJsonList<FlowiseFileUploadDto>(entity.FileUploadsJson),
        Id = entity.Id,
        Message = entity.Message,
        Role = entity.Role,
        AgentExecutedData = ParseJsonList<FlowiseAgentExecutedNodeDto>(entity.AgentExecutedDataJson),
        AgentReasoning = ParseJsonList<FlowiseAgentReasoningDto>(entity.AgentReasoningJson),
        ArtifactsJson = NormalizeJsonArray(entity.ArtifactsJson),
        ActionJson = entity.ActionJson,
        FollowUpPrompts = ParseJsonList<string>(entity.FollowUpPromptsJson ?? "[]"),
        SourceDocuments = ParseJsonList<FlowiseSourceDocumentDto>(entity.SourceDocumentsJson),
        UsedTools = ParseJsonList<FlowiseUsedToolDto>(entity.UsedToolsJson)
    };

    private static FlowiseFeedbackDto MapFeedback(FlowiseFeedbackEntity entity) => new()
    {
        Id = entity.Id,
        MessageId = entity.MessageId,
        Rating = entity.Rating,
        Reason = entity.Reason
    };

    private static FlowiseLeadDto MapLead(FlowiseLeadEntity entity) => new()
    {
        ContactJson = entity.ContactJson,
        CreatedTime = entity.CreatedTime,
        Id = entity.Id,
        ResourceId = entity.ResourceId
    };

    private static IReadOnlyList<T> ParseJsonList<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<string?> GenerateFollowUpPromptsJsonAsync(FlowiseChatFlowEntity chatflow, string answer, CancellationToken cancellationToken)
    {
        var config = ReadFollowUpPromptsConfig(chatflow);
        if (config is null || string.IsNullOrWhiteSpace(answer))
        {
            return null;
        }

        try
        {
            var endpoint = await modelRouter.ResolveAsync(null, cancellationToken);
            var prompt = config.Prompt.Replace("{history}", answer, StringComparison.Ordinal);
            var response = await chatRuntime.CompleteAsync(new AiKernelChatRequest
            {
                AgentName = $"{chatflow.Name} Follow-up Prompts",
                Endpoint = endpoint,
                JsonResponse = true,
                Messages =
                [
                    new ChatMessageContent(AuthorRole.System, "Return JSON only with shape {\"questions\":[\"...\",\"...\",\"...\"]}."),
                    new ChatMessageContent(AuthorRole.User, prompt)
                ]
            }, cancellationToken);
            var questions = ExtractFollowUpQuestions(response);
            return questions.Count == 0 ? null : JsonSerializer.Serialize(questions);
        }
        catch
        {
            return null;
        }
    }

    private static FollowUpPromptConfig? ReadFollowUpPromptsConfig(FlowiseChatFlowEntity chatflow)
    {
        using var configDocument = TryParseDocument(chatflow.FollowUpPrompts);
        using var chatbotDocument = TryParseDocument(chatflow.ChatbotConfig);
        var chatbotFollowUpPrompts = TryGetObjectProperty(chatbotDocument?.RootElement, "followUpPrompts");
        var enabled = IsFollowUpPromptsEnabled(configDocument?.RootElement) || IsFollowUpPromptsEnabled(chatbotFollowUpPrompts);
        if (!enabled)
        {
            return null;
        }

        var prompt = ReadFollowUpPrompt(configDocument?.RootElement) ?? ReadFollowUpPrompt(chatbotFollowUpPrompts);
        return new FollowUpPromptConfig(prompt ?? DefaultFollowUpPrompt);
    }

    private static JsonElement? TryGetObjectProperty(JsonElement? element, string propertyName)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return element.Value.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.Object
            ? property
            : null;
    }

    private static bool IsFollowUpPromptsEnabled(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return element.Value.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.True;
    }

    private static string? ReadFollowUpPrompt(JsonElement? element)
    {
        if (element is null || element.Value.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var providerName = element.Value.TryGetProperty("selectedProvider", out var selectedProvider) ? selectedProvider.GetString() : null;
        if (!string.IsNullOrWhiteSpace(providerName) && element.Value.TryGetProperty(providerName, out var provider) && provider.ValueKind == JsonValueKind.Object)
        {
            if (provider.TryGetProperty("prompt", out var providerPrompt) && providerPrompt.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(providerPrompt.GetString()))
            {
                return providerPrompt.GetString();
            }
        }

        return element.Value.TryGetProperty("prompt", out var prompt) && prompt.ValueKind == JsonValueKind.String
            ? prompt.GetString()
            : null;
    }

    private static List<string> ExtractFollowUpQuestions(string response)
    {
        try
        {
            using var document = JsonDocument.Parse(response);
            if (!document.RootElement.TryGetProperty("questions", out var questions) || questions.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return questions.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Take(3)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static JsonDocument? TryParseDocument(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record FollowUpPromptConfig(string Prompt);

    private static IReadOnlyList<FlowiseFileUploadDto> NormalizeUploads(IReadOnlyList<FlowiseFileUploadDto>? uploads)
    {
        if (uploads is null || uploads.Count == 0)
        {
            return [];
        }

        if (uploads.Count > MaxFileUploadCount)
        {
            throw new ValidationException($"Flowise 单次最多允许上传 {MaxFileUploadCount} 个文件", ErrorCodes.ParameterInvalid);
        }

        return uploads.Select(NormalizeUpload).ToList();
    }

    private static FlowiseFileUploadDto NormalizeUpload(FlowiseFileUploadDto upload)
    {
        if (string.IsNullOrWhiteSpace(upload.Name) || string.IsNullOrWhiteSpace(upload.Data) || string.IsNullOrWhiteSpace(upload.Mime))
        {
            throw new ValidationException("Flowise 上传文件缺少 name、data 或 mime", ErrorCodes.ParameterInvalid);
        }

        if (upload.Data.Length > MaxFileUploadDataLength)
        {
            throw new ValidationException("Flowise 上传文件超过允许大小", ErrorCodes.ParameterInvalid);
        }

        return new FlowiseFileUploadDto
        {
            Data = upload.Data.Trim(),
            Mime = upload.Mime.Trim(),
            Name = upload.Name.Trim(),
            Type = string.IsNullOrWhiteSpace(upload.Type) ? ResolveUploadType(upload.Mime) : upload.Type.Trim()
        };
    }

    private static string ResolveUploadType(string mime) =>
        mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
            ? "file:image"
            : mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)
                ? "audio"
                : "file:full";

    private static string NormalizePredictionQuestion(FlowisePredictionRequest request)
    {
        var action = TryReadHumanInputAction(request.Form);
        if (action is not null)
        {
            return action.Label;
        }

        var question = NormalizeOptional(request.Question);
        if (!string.IsNullOrWhiteSpace(question))
        {
            return question;
        }

        if (request.Form is null || request.Form.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(
            Environment.NewLine,
            request.Form
                .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.Equals(item.Key, "action", StringComparison.OrdinalIgnoreCase))
                .Select(item => $"{item.Key}: {FormatFormValue(item.Value)}"));
    }

    private static bool HasFormInput(FlowisePredictionRequest request) =>
        request.Form is not null && request.Form.Any(item => !string.Equals(item.Key, "action", StringComparison.OrdinalIgnoreCase));

    private async Task<FlowiseHumanInputResumeRequest?> ResolveHumanInputResumeAsync(
        string resourceId,
        string chatId,
        FlowisePredictionRequest request,
        CancellationToken cancellationToken)
    {
        var action = TryReadHumanInputAction(request.Form);
        if (action is null)
        {
            return null;
        }

        var previousExecution = await db.Queryable<FlowiseExecutionEntity>()
            .Where(item => !item.IsDeleted && item.ResourceId == resourceId && item.Status == "Stopped")
            .Where(item => item.InputJson.Contains($"\"chatId\":\"{chatId}\"") || item.InputJson.Contains($"\"chatId\": \"{chatId}\""))
            .OrderBy(item => item.CreatedTime, OrderByType.Desc)
            .FirstAsync(cancellationToken);
        if (previousExecution is null)
        {
            throw new ValidationException("未找到可恢复的 Human Input 执行记录", ErrorCodes.ParameterInvalid);
        }

        var previousActionId = ReadJsonString(previousExecution.ActionJson, "id");
        if (!string.IsNullOrWhiteSpace(previousActionId) && !string.Equals(previousActionId, action.ActionId, StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("Human Input action 与上一条停止执行不匹配", ErrorCodes.ParameterInvalid);
        }

        var previousExecutionDataJson = ExtractJsonArray(previousExecution.OutputJson, "agentExecutedData", "AgentExecutedData");
        if (!ExecutionDataContainsStoppedNode(previousExecutionDataJson, action.NodeId))
        {
            throw new ValidationException("Human Input 停止节点与上一条执行记录不匹配", ErrorCodes.ParameterInvalid);
        }

        action.PreviousExecutionId = previousExecution.Id;
        action.PreviousExecutionDataJson = previousExecutionDataJson;
        action.PreviousActionJson = string.IsNullOrWhiteSpace(previousExecution.ActionJson) ? "{}" : previousExecution.ActionJson;
        return action;
    }

    private static FlowiseHumanInputResumeRequest? TryReadHumanInputAction(IReadOnlyDictionary<string, object?>? form)
    {
        if (form is null || !form.TryGetValue("action", out var rawAction) || rawAction is null)
        {
            return null;
        }

        using var document = JsonDocument.Parse(JsonSerializer.Serialize(rawAction));
        var root = document.RootElement;
        var actionId = ReadString(root, "actionId");
        var choice = ReadString(root, "choice");
        var label = ReadString(root, "label");
        var nodeId = ReadString(root, "nodeId");
        var nodeLabel = ReadString(root, "nodeLabel");
        if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(choice) || string.IsNullOrWhiteSpace(nodeId))
        {
            return null;
        }

        return new FlowiseHumanInputResumeRequest
        {
            ActionId = actionId.Trim(),
            Choice = choice.Trim(),
            Label = string.IsNullOrWhiteSpace(label) ? choice.Trim() : label.Trim(),
            NodeId = nodeId.Trim(),
            NodeLabel = string.IsNullOrWhiteSpace(nodeLabel) ? "Human Input" : nodeLabel.Trim()
        };
    }

    private static bool ExecutionDataContainsStoppedNode(string executionDataJson, string nodeId)
    {
        try
        {
            using var document = JsonDocument.Parse(executionDataJson);
            return document.RootElement.ValueKind == JsonValueKind.Array &&
                document.RootElement.EnumerateArray().Any(item =>
                    ReadString(item, "nodeId") == nodeId &&
                    string.Equals(ReadString(item, "status"), "STOPPED", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadJsonString(string? json, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadString(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        TryGetPropertyIgnoreCase(element, propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        return false;
    }

    private static string FormatFormValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is JsonElement jsonElement)
        {
            return jsonElement.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.String => jsonElement.GetString() ?? string.Empty,
                JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => jsonElement.ToString(),
                _ => jsonElement.GetRawText()
            };
        }

        return Convert.ToString(value, global::System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string ExtractResponseText(string outputJson)
    {
        try
        {
            using var document = JsonDocument.Parse(outputJson);
            if (TryGetProperty(document.RootElement, "answer", "Answer", out var answer))
            {
                return answer.GetString() ?? outputJson;
            }

            if (TryGetProperty(document.RootElement, "status", "Status", out var status))
            {
                return status.GetString() ?? outputJson;
            }
        }
        catch (JsonException)
        {
            return outputJson;
        }

        return outputJson;
    }

    private static string ExtractJsonArray(string outputJson, string camelCaseName, string pascalCaseName)
    {
        try
        {
            using var document = JsonDocument.Parse(outputJson);
            return TryGetProperty(document.RootElement, camelCaseName, pascalCaseName, out var value) && value.ValueKind == JsonValueKind.Array
                ? value.GetRawText()
                : "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static bool TryGetProperty(JsonElement element, string camelCaseName, string pascalCaseName, out JsonElement value) =>
        element.TryGetProperty(camelCaseName, out value) || element.TryGetProperty(pascalCaseName, out value);

    private static string NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "{}";
        }

        using var _ = JsonDocument.Parse(value);
        return value.Trim();
    }

    private static string NormalizeJsonArray(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "[]";
        }

        try
        {
            using var document = JsonDocument.Parse(value);
            return document.RootElement.ValueKind == JsonValueKind.Array ? value.Trim() : "[]";
        }
        catch (JsonException)
        {
            return "[]";
        }
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool ContactMatchesChat(string contactJson, string chatId)
    {
        try
        {
            using var document = JsonDocument.Parse(contactJson);
            return document.RootElement.TryGetProperty("chatId", out var property)
                && string.Equals(property.GetString(), chatId, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int NormalizePageIndex(int pageIndex) => Math.Max(pageIndex, 1);

    private static int NormalizePageSize(int pageSize) => Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

    private static async Task WriteSseEventAsync(HttpResponse response, string eventName, object? data, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new { data, @event = eventName }, SseJsonOptions);
        await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
        await response.Body.FlushAsync(cancellationToken);
    }
}
