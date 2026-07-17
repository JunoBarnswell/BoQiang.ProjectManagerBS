using System.Text.Json;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionContentParser(
    FlowiseRuntimeNodeDataReader nodeDataReader)
{
    private static readonly JsonSerializerOptions CaseInsensitiveJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal IReadOnlyList<LlmMessageDto> ReadLlmConfiguredMessages(IReadOnlyDictionary<string, JsonElement> data)
        => ReadConfiguredMessages(data, "llmMessages", static (role, content) => new LlmMessageDto(role, content));

    internal IReadOnlyList<AgentMessageDto> ReadAgentConfiguredMessages(IReadOnlyDictionary<string, JsonElement> data)
        => ReadConfiguredMessages(data, "agentMessages", static (role, content) => new AgentMessageDto(role, content));

    internal IReadOnlyList<string> ReadLlmStructuredOutputFields(IReadOnlyDictionary<string, JsonElement> data)
        => ReadStructuredOutputFields(data, "llmStructuredOutput");

    internal IReadOnlyList<string> ReadAgentStructuredOutputFields(IReadOnlyDictionary<string, JsonElement> data)
        => ReadStructuredOutputFields(data, "agentStructuredOutput");

    internal bool TryParseJsonObject(string content, out IReadOnlyDictionary<string, object?> value)
    {
        value = new Dictionary<string, object?>();
        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            value = JsonSerializer.Deserialize<Dictionary<string, object?>>(document.RootElement.GetRawText(), CaseInsensitiveJsonOptions)
                ?? new Dictionary<string, object?>();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal AuthorRole ToAuthorRole(string? role)
    {
        if (role?.Equals("assistant", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AuthorRole.Assistant;
        }

        if (role?.Equals("system", StringComparison.OrdinalIgnoreCase) == true ||
            role?.Equals("developer", StringComparison.OrdinalIgnoreCase) == true)
        {
            return AuthorRole.System;
        }

        return AuthorRole.User;
    }

    private IReadOnlyList<TMessage> ReadConfiguredMessages<TMessage>(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName,
        Func<string, string, TMessage> createMessage)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, propertyName, out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var document = JsonDocument.Parse(value.GetString() ?? "[]");
                return ReadMessageArray(document.RootElement, createMessage);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return ReadMessageArray(value, createMessage);
    }

    private static IReadOnlyList<TMessage> ReadMessageArray<TMessage>(
        JsonElement value,
        Func<string, string, TMessage> createMessage)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var messages = new List<TMessage>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var role = FlowiseJsonElementReader.ReadString(item, "role") ?? "user";
            var content = FlowiseJsonElementReader.ReadJsonPropertyAsString(item, "content");
            if (!string.IsNullOrWhiteSpace(content))
            {
                messages.Add(createMessage(role, content));
            }
        }

        return messages;
    }

    private IReadOnlyList<string> ReadStructuredOutputFields(
        IReadOnlyDictionary<string, JsonElement> data,
        string propertyName)
    {
        if (!nodeDataReader.TryGetNodeInputValue(data, propertyName, out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            try
            {
                using var document = JsonDocument.Parse(value.GetString() ?? "[]");
                return ReadStructuredOutputFieldArray(document.RootElement);
            }
            catch (JsonException)
            {
                return [];
            }
        }

        return ReadStructuredOutputFieldArray(value);
    }

    private static IReadOnlyList<string> ReadStructuredOutputFieldArray(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.Object)
            .Select(item => FlowiseJsonElementReader.ReadString(item, "key"))
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
