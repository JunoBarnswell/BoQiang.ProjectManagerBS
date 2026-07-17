using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using AsterERP.Api.Infrastructure.Ai;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using AsterERP.Shared;
using AsterERP.Shared.Exceptions;
using Microsoft.AspNetCore.Http;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseWebhookListenerService(
    IServiceScopeFactory scopeFactory,
    FlowisePermissionGuard permissionGuard,
    IFlowiseResourceAccessGuard resourceAccessGuard) : IFlowiseWebhookListenerService
{
    private static readonly ConcurrentDictionary<string, FlowiseWebhookListenerSession> Sessions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FlowiseWebhookListenerRegistrationDto> RegisterAsync(string chatflowId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWebhook, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
        await EnsureChatflowExistsAsync(chatflowId, cancellationToken);

        var normalizedChatflowId = NormalizeChatflowId(chatflowId);
        var listenerId = Guid.NewGuid().ToString("N");
        var session = new FlowiseWebhookListenerSession(normalizedChatflowId, listenerId);
        Sessions[BuildKey(normalizedChatflowId, listenerId)] = session;
        await session.WriteAsync("listenerReady", new { chatflowId = normalizedChatflowId, listenerId }, cancellationToken);

        return new FlowiseWebhookListenerRegistrationDto
        {
            ChatflowId = normalizedChatflowId,
            CreatedAt = session.CreatedAt,
            ListenerId = listenerId
        };
    }

    public Task<bool> UnregisterAsync(string chatflowId, string listenerId, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWebhook, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
        if (Sessions.TryRemove(BuildKey(chatflowId, listenerId), out var session))
        {
            session.Complete();
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public async Task StreamAsync(string chatflowId, string listenerId, HttpResponse response, CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWebhook, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
        if (!Sessions.TryGetValue(BuildKey(chatflowId, listenerId), out var session))
        {
            throw new ValidationException("Flowise webhook listener 不存在或已关闭", ErrorCodes.ParameterInvalid);
        }

        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache";
        response.Headers.Connection = "keep-alive";
        await response.StartAsync(cancellationToken);

        await foreach (var item in session.ReadAllAsync(cancellationToken))
        {
            var payload = JsonSerializer.Serialize(new { data = item.Data, @event = item.EventName }, JsonOptions);
            await response.WriteAsync($"event: {item.EventName}\n", cancellationToken);
            await response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await response.Body.FlushAsync(cancellationToken);
        }
    }

    public async Task<FlowiseWebhookTriggerResponse> TriggerAsync(
        string chatflowId,
        FlowiseWebhookTriggerRequest request,
        string? webhookSecret,
        CancellationToken cancellationToken)
    {
        permissionGuard.EnsureAny(PermissionCodes.FlowiseWebhook, PermissionCodes.FlowiseRun, PermissionCodes.FlowiseManage);
        var normalizedChatflowId = NormalizeChatflowId(chatflowId);
        var session = Sessions.Values
            .Where(item => string.Equals(item.ChatflowId, normalizedChatflowId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault()
            ?? throw new ValidationException("Flowise webhook listener 未开启", ErrorCodes.ParameterInvalid);

        await using var scope = scopeFactory.CreateAsyncScope();
        var secretProtector = scope.ServiceProvider.GetRequiredService<IAiSecretProtector>();
        var executionService = scope.ServiceProvider.GetRequiredService<IFlowiseExecutionService>();
        var chatflow = await resourceAccessGuard.GetChatflowForCurrentWorkspaceAsync(normalizedChatflowId, cancellationToken);

        ValidateWebhookSecret(chatflow, secretProtector, webhookSecret);

        object? endPayload = null;
        await session.WriteAsync("metadata", new { chatflowId = normalizedChatflowId, listenerId = session.ListenerId }, cancellationToken);
        var webhookContext = NormalizeWebhookContext(request.InputJson);
        var execution = await executionService.StreamAsync(new FlowiseExecutionStartRequest
        {
            ChatId = request.ChatId,
            IdempotencyKey = $"{normalizedChatflowId}:webhook:{Guid.NewGuid():N}",
            InputJson = NormalizeInputJson(request.InputJson, request.Question, request.ChatId, request.SessionId),
            Question = request.Question,
            ResourceId = normalizedChatflowId,
            SessionId = request.SessionId,
            Webhook = webhookContext
        }, async (eventName, data, token) =>
        {
            if (string.Equals(eventName, "start", StringComparison.Ordinal))
            {
                await session.WriteAsync("agentFlowEvent", "INPROGRESS", token);
            }

            if (string.Equals(eventName, "end", StringComparison.Ordinal))
            {
                endPayload = data;
                await session.WriteAsync("agentFlowEvent", "FINISHED", token);
            }

            await session.WriteAsync(eventName, data, token);
        }, cancellationToken);

        await session.WriteAsync("executionEnd", endPayload ?? execution, CancellationToken.None);
        return new FlowiseWebhookTriggerResponse
        {
            ChatflowId = normalizedChatflowId,
            ListenerId = session.ListenerId,
            Status = execution.Status,
            TraceId = execution.TraceId
        };
    }

    private async Task EnsureChatflowExistsAsync(string chatflowId, CancellationToken cancellationToken)
    {
        await resourceAccessGuard.EnsureChatflowForCurrentWorkspaceAsync(chatflowId, cancellationToken);
    }

    private static void ValidateWebhookSecret(FlowiseChatFlowEntity chatflow, IAiSecretProtector secretProtector, string? webhookSecret)
    {
        if (!chatflow.WebhookSecretConfigured || string.IsNullOrWhiteSpace(chatflow.WebhookSecretCipherText))
        {
            return;
        }

        var expected = secretProtector.Unprotect(chatflow.WebhookSecretCipherText);
        if (!string.Equals(expected, webhookSecret?.Trim(), StringComparison.Ordinal))
        {
            throw new ValidationException("Flowise webhook secret 无效", ErrorCodes.PermissionDenied);
        }
    }

    private static string NormalizeChatflowId(string chatflowId)
    {
        if (string.IsNullOrWhiteSpace(chatflowId))
        {
            throw new ValidationException("缺少 Flowise ChatFlow Id", ErrorCodes.ParameterInvalid);
        }

        return chatflowId.Trim();
    }

    private static string BuildKey(string chatflowId, string listenerId)
    {
        if (string.IsNullOrWhiteSpace(listenerId))
        {
            throw new ValidationException("缺少 Flowise webhook listener Id", ErrorCodes.ParameterInvalid);
        }

        return $"{NormalizeChatflowId(chatflowId)}:{listenerId.Trim()}";
    }

    private static string NormalizeInputJson(string? inputJson, string? question, string? chatId, string? sessionId)
    {
        var record = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["question"] = question,
            ["chatId"] = chatId,
            ["sessionId"] = sessionId
        };

        if (!string.IsNullOrWhiteSpace(inputJson))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<string, object?>>(inputJson);
                if (parsed is not null)
                {
                    foreach (var item in parsed)
                    {
                        record[item.Key] = item.Value;
                    }
                }
            }
            catch
            {
                record["rawInput"] = inputJson;
            }
        }

        return JsonSerializer.Serialize(record, JsonOptions);
    }

    private static Dictionary<string, object?> NormalizeWebhookContext(string? inputJson)
    {
        var body = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var headers = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var query = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(inputJson))
        {
            try
            {
                using var document = JsonDocument.Parse(inputJson);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    body = ReadWebhookSection(document.RootElement, "body") ?? JsonSerializer.Deserialize<Dictionary<string, object?>>(document.RootElement.GetRawText(), JsonOptions) ?? body;
                    headers = ReadWebhookSection(document.RootElement, "headers") ?? headers;
                    query = ReadWebhookSection(document.RootElement, "query") ?? query;
                }
                else
                {
                    body["payload"] = document.RootElement.Clone();
                }
            }
            catch (JsonException)
            {
                body["rawInput"] = inputJson;
            }
        }

        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["body"] = body,
            ["headers"] = headers,
            ["query"] = query
        };
    }

    private static Dictionary<string, object?>? ReadWebhookSection(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var section) || section.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(section.GetRawText(), JsonOptions);
    }

    private sealed class FlowiseWebhookListenerSession(string chatflowId, string listenerId)
    {
        private readonly Channel<FlowiseWebhookListenerEvent> channel = Channel.CreateUnbounded<FlowiseWebhookListenerEvent>();

        public string ChatflowId { get; } = chatflowId;

        public DateTime CreatedAt { get; } = DateTime.UtcNow;

        public string ListenerId { get; } = listenerId;

        public ValueTask WriteAsync(string eventName, object? data, CancellationToken cancellationToken) =>
            channel.Writer.WriteAsync(new FlowiseWebhookListenerEvent(eventName, data), cancellationToken);

        public IAsyncEnumerable<FlowiseWebhookListenerEvent> ReadAllAsync(CancellationToken cancellationToken) =>
            channel.Reader.ReadAllAsync(cancellationToken);

        public void Complete() => channel.Writer.TryComplete();
    }

    private sealed record FlowiseWebhookListenerEvent(string EventName, object? Data);
}
