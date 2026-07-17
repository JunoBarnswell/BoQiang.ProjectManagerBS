using System.Text.Json;
using AsterERP.Api.Application.Ai.Tools;
using AsterERP.Api.Application.ApplicationDataCenter;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai;
using AsterERP.Contracts.Ai;
using AsterERP.Contracts.ApplicationDataCenter;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using SqlSugar;

namespace AsterERP.Api.Application.Ai;

public sealed class AiDataCenterAssistantService(
    ISqlSugarClient db,
    AiWorkspaceContext workspaceContext,
    IAiModelRouter modelRouter,
    AiKernelChatRuntime chatRuntime,
    AiKernelFunctionCatalog toolCatalog,
    ApplicationDataSourceService dataSourceService)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiDataCenterAssistantIntentResponse> ResolveIntentAsync(
        AiDataCenterAssistantIntentRequest request,
        CancellationToken cancellationToken)
    {
        ValidateRequest(request);
        var workspace = workspaceContext.Resolve();
        var dataSource = await EnsureDataSourceAsync(workspace, request.DataSourceId, cancellationToken);
        var endpoint = await modelRouter.ResolveAsync(request.ModelConfigId, cancellationToken);
        var conversation = await ResolveConversationAsync(workspace, request, dataSource, cancellationToken);
        var userMessage = await InsertUserMessageAsync(workspace, conversation, request.Content.Trim(), cancellationToken);
        var run = await InsertRunAsync(workspace, conversation, userMessage.Id, endpoint, request.Content, cancellationToken);

        try
        {
            var modelContent = await CompleteIntentAsync(workspace, conversation, dataSource, endpoint, request, cancellationToken);
            var response = BuildIntentResponse(conversation.Id, run.Id, userMessage.Id, endpoint.ModelConfigId, modelContent, request);
            var assistantMessage = await InsertAssistantMessageAsync(workspace, conversation, run.Id, response, cancellationToken);
            response.AssistantMessageId = assistantMessage.Id;
            await MarkRunSucceededAsync(conversation, run, assistantMessage, endpoint, cancellationToken);
            return response;
        }
        catch (Exception ex)
        {
            await MarkRunFailedAsync(conversation, run, ex, CancellationToken.None);
            throw;
        }
    }

    private async Task<string> CompleteIntentAsync(
        AiWorkspace workspace,
        AiConversationEntity conversation,
        ApplicationDataCenterObjectDetailResponse dataSource,
        AiModelEndpoint endpoint,
        AiDataCenterAssistantIntentRequest request,
        CancellationToken cancellationToken)
    {
        var messages = new List<ChatMessageContent>
        {
            new(AuthorRole.System, BuildSystemPrompt(dataSource, request)),
        };
        messages.AddRange(await BuildRecentHistoryAsync(conversation.Id, cancellationToken));
        messages.Add(new ChatMessageContent(AuthorRole.User, request.Content.Trim()));

        return await chatRuntime.CompleteAsync(new AiKernelChatRequest
        {
            AgentName = "DataCenterAssistant",
            Endpoint = endpoint,
            JsonResponse = true,
            Messages = messages,
            UserId = workspace.UserId
        }, cancellationToken);
    }

    private static string BuildSystemPrompt(
        ApplicationDataCenterObjectDetailResponse dataSource,
        AiDataCenterAssistantIntentRequest request)
    {
        var selectedTable = string.IsNullOrWhiteSpace(request.SelectedTable) ? "未选择" : request.SelectedTable.Trim();
        return $$$$"""
        你是 AsterERP 数据中心数据库工作台助手。你必须只返回一个 JSON 对象，不要 Markdown，不要解释性前后缀。

        当前数据库：
        - dataSourceId: {{{{request.DataSourceId}}}}
        - name: {{{{dataSource.ObjectName}}}}
        - code: {{{{dataSource.ObjectCode}}}}
        - type: {{{{dataSource.ObjectType}}}}
        - selectedTable: {{{{selectedTable}}}}

        JSON 输出结构：
        {
          "replyText": "给用户看的简短说明",
          "toolIntents": [
            {
              "toolCode": "dataCenter.table.search",
              "summary": "准备执行的动作",
              "arguments": {}
            }
          ]
        }

        可用工具和参数：
        - dataCenter.table.search: {"dataSourceId":"当前 dataSourceId","keyword":""}
        - dataCenter.table.describe: {"dataSourceId":"当前 dataSourceId","tableName":"表名"}
        - dataCenter.table.queryRows: {"dataSourceId":"当前 dataSourceId","tableName":"表名","request":{"pageIndex":1,"pageSize":20,"keyword":""}}
        - dataCenter.table.insertRow: {"dataSourceId":"当前 dataSourceId","tableName":"表名","values":{"字段名":"字段值"}}
        - dataCenter.table.updateRow: {"dataSourceId":"当前 dataSourceId","tableName":"表名","keyValues":{"主键字段":"主键值"},"values":{"要更新字段":"新值"}}
        - dataCenter.table.deleteRow: {"dataSourceId":"当前 dataSourceId","tableName":"表名","keyValues":{"主键字段":"主键值"}}
        - dataCenter.table.create: {"dataSourceId":"当前 dataSourceId","request":{"schemaName":"","tableName":"表名","alias":"中文别名","remark":"说明","columns":[{"columnName":"id","dataType":"TEXT","nullable":false,"primaryKey":true,"defaultValue":"","remark":""}]}}
        - dataCenter.view.create: {"dataSourceId":"当前 dataSourceId","request":{"schemaName":"","viewName":"视图名","alias":"中文别名","remark":"说明","sql":"SELECT ..."}}
        - dataCenter.view.update: {"dataSourceId":"当前 dataSourceId","viewId":"视图ID","request":{"schemaName":"","viewName":"视图名","alias":"中文别名","remark":"说明","sql":"SELECT ..."}}
        - dataCenter.view.preview: {"dataSourceId":"当前 dataSourceId","sql":"SELECT ...","maxRows":20}
        - dataCenter.mappingCache.create: {"dataSourceId":"当前 dataSourceId","request":{"cacheKey":"缓存KEY","cacheName":"缓存名称","sql":"SELECT id as key, name as value FROM 表名","remark":""}}
        - dataCenter.mappingCache.refresh: {"dataSourceId":"当前 dataSourceId","cacheId":"缓存ID"}

        规则：
        - 复合请求可以返回多个 toolIntents，必须按依赖顺序排列，前一步成功后再执行后一步。
        - 用户要求“创建多个表、插入数据、多个表组成视图、接口返回数据”时，按顺序生成：多条 dataCenter.table.create、多条 dataCenter.table.insertRow、dataCenter.view.create、dataCenter.view.preview；接口发布、变量、if/for 和 CRUD 编排必须提示进入微流控制台完成。
        - 建多张相关表时，优先使用可读的业务表名和字段名；SQLite 场景优先使用 TEXT/INTEGER/REAL/DATETIME 类型，主键建议 TEXT 或 INTEGER。
        - 生成 join 视图时，SQL 必须完整使用已创建表名，并给字段起清晰别名，示例：SELECT u.id AS user_id, u.name AS user_name, o.amount AS order_amount FROM users u JOIN orders o ON o.user_id = u.id。
        - 插入数据时，values 只放字段和值，不要包裹 request。
        - 如果用户请求建表、写行、建视图、创建缓存，只返回 toolIntents，不要声称已执行。
        - 如果用户请求发布接口、领域对象建模或 CRUD 编排，toolIntents 为空，replyText 引导到“数据中心 / 微流管理”使用可视化设计器配置。
        - 如果用户请求查询/预览，也返回 toolIntents。
        - 不确定参数时，toolIntents 为空，replyText 说明缺少哪些信息。
        - 所有工具参数必须包含当前 dataSourceId。
        """;
    }

    private async Task<IReadOnlyList<ChatMessageContent>> BuildRecentHistoryAsync(
        string conversationId,
        CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .OrderBy(item => item.Seq, OrderByType.Desc)
            .Take(8)
            .ToListAsync(cancellationToken);
        return rows.OrderBy(item => item.Seq)
            .Where(item => item.Role is "user" or "assistant")
            .Select(item => new ChatMessageContent(ToAuthorRole(item.Role), item.Content))
            .ToList();
    }

    private AiDataCenterAssistantIntentResponse BuildIntentResponse(
        string conversationId,
        string runId,
        string userMessageId,
        string modelConfigId,
        string modelContent,
        AiDataCenterAssistantIntentRequest request)
    {
        using var document = JsonDocument.Parse(ExtractJsonObject(modelContent));
        var root = document.RootElement;
        var replyText = ReadString(root, "replyText") ?? "已分析你的数据中心操作请求。";
        var intents = ReadToolIntents(root, request.DataSourceId);
        return new AiDataCenterAssistantIntentResponse
        {
            ConversationId = conversationId,
            RunId = runId,
            UserMessageId = userMessageId,
            ModelConfigId = modelConfigId,
            ReplyText = replyText,
            ToolIntents = intents
        };
    }

    private IReadOnlyList<AiDataCenterAssistantToolIntentDto> ReadToolIntents(
        JsonElement root,
        string dataSourceId)
    {
        if (!root.TryGetProperty("toolIntents", out var intentsElement) ||
            intentsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var allowedTools = toolCatalog.ListDefinitions()
            .Where(item => string.Equals(item.ToolDomain, "data-center", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(item => item.ToolCode, StringComparer.OrdinalIgnoreCase);
        var intents = new List<AiDataCenterAssistantToolIntentDto>();
        foreach (var element in intentsElement.EnumerateArray())
        {
            var toolCode = ReadString(element, "toolCode");
            if (string.IsNullOrWhiteSpace(toolCode) ||
                !allowedTools.TryGetValue(toolCode, out var definition))
            {
                continue;
            }

            var arguments = ReadArguments(element, dataSourceId);
            intents.Add(new AiDataCenterAssistantToolIntentDto
            {
                ToolCode = definition.ToolCode,
                ToolName = definition.ToolName,
                Summary = FirstNonEmpty(ReadString(element, "summary"), definition.ToolName),
                RiskLevel = definition.RiskLevel,
                RequiresConfirmation = definition.RequiresConfirmation,
                Arguments = arguments,
                ArgumentsJson = JsonSerializer.Serialize(arguments, JsonOptions)
            });
        }

        return intents;
    }

    private static Dictionary<string, object?> ReadArguments(JsonElement element, string dataSourceId)
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (element.TryGetProperty("arguments", out var argumentsElement) &&
            argumentsElement.ValueKind == JsonValueKind.Object)
        {
            arguments = JsonSerializer.Deserialize<Dictionary<string, object?>>(argumentsElement.GetRawText(), JsonOptions)
                        ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        arguments["dataSourceId"] = dataSourceId;
        return arguments;
    }

    private async Task<AiConversationEntity> ResolveConversationAsync(
        AiWorkspace workspace,
        AiDataCenterAssistantIntentRequest request,
        ApplicationDataCenterObjectDetailResponse dataSource,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return await db.Queryable<AiConversationEntity>()
                .FirstAsync(item => !item.IsDeleted &&
                                    item.Id == request.ConversationId &&
                                    item.TenantId == workspace.TenantId &&
                                    item.AppCode == workspace.AppCode &&
                                    item.OwnerUserId == workspace.UserId,
                    cancellationToken)
                   ?? throw new NotFoundException("会话不存在", ErrorCodes.AiConversationNotFound);
        }

        var title = BuildConversationTitle(dataSource, request.Content);
        var entity = new AiConversationEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            Title = title,
            Status = "Active",
            LastMessageAt = DateTime.UtcNow
        };
        await db.Insertable(entity).ExecuteCommandAsync(cancellationToken);
        return entity;
    }

    private async Task<AiMessageEntity> InsertUserMessageAsync(
        AiWorkspace workspace,
        AiConversationEntity conversation,
        string content,
        CancellationToken cancellationToken)
    {
        var message = new AiMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = conversation.Id,
            Role = "user",
            Seq = await NextSeqAsync(conversation.Id, cancellationToken),
            Content = content,
            TokenCount = EstimateTokens(content),
            Status = "Completed"
        };
        await db.Insertable(message).ExecuteCommandAsync(cancellationToken);
        return message;
    }

    private async Task<AiMessageEntity> InsertAssistantMessageAsync(
        AiWorkspace workspace,
        AiConversationEntity conversation,
        string runId,
        AiDataCenterAssistantIntentResponse response,
        CancellationToken cancellationToken)
    {
        var metadata = new
        {
            type = "dataCenterAssistantIntent",
            response.ModelConfigId,
            response.ToolIntents
        };
        var message = new AiMessageEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = conversation.Id,
            RunId = runId,
            Role = "assistant",
            Seq = await NextSeqAsync(conversation.Id, cancellationToken),
            Content = response.ReplyText,
            MetadataJson = JsonSerializer.Serialize(metadata, JsonOptions),
            TokenCount = EstimateTokens(response.ReplyText),
            FinishReason = response.ToolIntents.Count == 0 ? "no_tool_intent" : "tool_intent_ready",
            Status = "Completed"
        };
        await db.Insertable(message).ExecuteCommandAsync(cancellationToken);
        return message;
    }

    private async Task<AiChatRunEntity> InsertRunAsync(
        AiWorkspace workspace,
        AiConversationEntity conversation,
        string userMessageId,
        AiModelEndpoint endpoint,
        string content,
        CancellationToken cancellationToken)
    {
        var run = new AiChatRunEntity
        {
            TenantId = workspace.TenantId,
            AppCode = workspace.AppCode,
            OwnerUserId = workspace.UserId,
            ConversationId = conversation.Id,
            UserMessageId = userMessageId,
            ProviderId = endpoint.ProviderId,
            ModelConfigId = endpoint.ModelConfigId,
            Mode = "DataCenterAssistant",
            Status = "Running",
            RequestHash = content.GetHashCode(StringComparison.Ordinal).ToString(),
            TraceId = Guid.NewGuid().ToString("N"),
            StartedAt = DateTime.UtcNow
        };
        await db.Insertable(run).ExecuteCommandAsync(cancellationToken);
        return run;
    }

    private async Task MarkRunSucceededAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        AiMessageEntity assistant,
        AiModelEndpoint endpoint,
        CancellationToken cancellationToken)
    {
        run.AssistantMessageId = assistant.Id;
        run.Status = "Succeeded";
        run.PromptTokens = assistant.TokenCount;
        run.CompletionTokens = assistant.TokenCount;
        run.TotalTokens = assistant.TokenCount;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(run).ExecuteCommandAsync(cancellationToken);

        conversation.LastRunStatus = "Succeeded";
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.UpdatedTime = DateTime.UtcNow;
        conversation.Summary ??= $"数据中心助手会话，模型 {endpoint.ModelCode}";
        await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
    }

    private async Task MarkRunFailedAsync(
        AiConversationEntity conversation,
        AiChatRunEntity run,
        Exception error,
        CancellationToken cancellationToken)
    {
        run.Status = "Failed";
        run.ErrorCode = ResolveErrorCode(error).ToString();
        run.ErrorMessage = error.Message;
        run.CompletedAt = DateTime.UtcNow;
        run.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(run).ExecuteCommandAsync(cancellationToken);

        conversation.LastRunStatus = "Failed";
        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.UpdatedTime = DateTime.UtcNow;
        await db.Updateable(conversation).ExecuteCommandAsync(cancellationToken);
    }

    private async Task<ApplicationDataCenterObjectDetailResponse> EnsureDataSourceAsync(
        AiWorkspace workspace,
        string dataSourceId,
        CancellationToken cancellationToken)
    {
        var dataSource = await dataSourceService.GetAsync(dataSourceId, cancellationToken);
        if (!string.Equals(dataSource.Id, dataSourceId, StringComparison.OrdinalIgnoreCase))
        {
            throw new NotFoundException("数据源不存在", ErrorCodes.ApplicationDataCenterObjectNotFound);
        }

        return dataSource;
    }

    private async Task<int> NextSeqAsync(string conversationId, CancellationToken cancellationToken)
    {
        var rows = await db.Queryable<AiMessageEntity>()
            .Where(item => !item.IsDeleted && item.ConversationId == conversationId)
            .Select(item => item.Seq)
            .ToListAsync(cancellationToken);
        return rows.Count == 0 ? 1 : rows.Max() + 1;
    }

    private static void ValidateRequest(AiDataCenterAssistantIntentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DataSourceId))
        {
            throw new ValidationException("数据源不能为空", ErrorCodes.ParameterInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.Content))
        {
            throw new ValidationException("消息内容不能为空", ErrorCodes.ParameterInvalid);
        }
    }

    private static string ExtractJsonObject(string content)
    {
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            throw new ValidationException("模型未返回可解析的数据中心意图 JSON", ErrorCodes.AiModelServiceUnavailable);
        }

        return content[start..(end + 1)];
    }

    private static string? ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static AuthorRole ToAuthorRole(string role) =>
        role.Equals("assistant", StringComparison.OrdinalIgnoreCase) ? AuthorRole.Assistant : AuthorRole.User;

    private static string BuildConversationTitle(ApplicationDataCenterObjectDetailResponse dataSource, string content)
    {
        var summary = content.Trim().ReplaceLineEndings(" ");
        if (summary.Length > 24)
        {
            summary = summary[..24];
        }

        return $"数据中心 · {dataSource.ObjectName} · {summary}";
    }

    private static int EstimateTokens(string? content) => string.IsNullOrEmpty(content) ? 0 : Math.Max(1, content.Length / 4);

    private static int ResolveErrorCode(Exception error) =>
        error is BusinessException businessException ? businessException.Code : ErrorCodes.AiModelServiceUnavailable;

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item))?.Trim() ?? string.Empty;
}
