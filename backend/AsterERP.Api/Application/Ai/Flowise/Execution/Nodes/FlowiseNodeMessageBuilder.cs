using System.Text;
using AsterERP.Api.Modules.Ai.Flowise;
using AsterERP.Contracts.Ai.Flowise;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseNodeMessageBuilder(
    FlowiseRuntimeNodeDataReader nodeDataReader,
    FlowiseExecutionContentParser executionContentParser,
    FlowiseExecutionTemplateResolver templateResolver)
{
    internal IReadOnlyList<ChatMessageContent> BuildLlmMessages(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults)
    {
        var messages = new List<ChatMessageContent>();
        foreach (var item in executionContentParser.ReadLlmConfiguredMessages(node.Data))
        {
            var content = templateResolver.ResolveLlmTemplate(item.Content, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            InsertByRole(messages, executionContentParser.ToAuthorRole(item.Role), content);
        }

        if (!messages.Any(message => message.Role == AuthorRole.System))
        {
            messages.Insert(0, new ChatMessageContent(AuthorRole.System, BuildSystemInstructions(chatflow, flowData, runtimeModelResults)));
        }

        if (nodeDataReader.ReadNodeInputBool(node.Data, "llmEnableMemory"))
        {
            AddMemoryMessages(
                messages,
                nodeDataReader.ReadNodeInputString(node.Data, "llmMemoryType"),
                nodeDataReader.ReadNodeInputInt(node.Data, "llmMemoryWindowSize"),
                context);
        }

        var userMessage = templateResolver.ResolveLlmTemplate(
            nodeDataReader.ReadNodeInputString(node.Data, "llmUserMessage") ?? string.Empty,
            context,
            runtimeModelResults,
            httpResults,
            executeFlowResults,
            customFunctionResults,
            llmResults);
        AppendUserMessage(messages, userMessage, context);
        return messages;
    }

    internal IReadOnlyList<ChatMessageContent> BuildAgentMessages(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults,
        AgentKnowledgeContext knowledgeContext,
        AgentToolContext toolContext)
    {
        var messages = new List<ChatMessageContent>();
        foreach (var item in executionContentParser.ReadAgentConfiguredMessages(node.Data))
        {
            var content = templateResolver.ResolveAgentTemplate(item.Content, context, runtimeModelResults, httpResults, executeFlowResults, customFunctionResults, llmResults, agentResults);
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            InsertByRole(messages, executionContentParser.ToAuthorRole(item.Role), content);
        }

        if (!messages.Any(message => message.Role == AuthorRole.System))
        {
            messages.Insert(0, new ChatMessageContent(AuthorRole.System, BuildSystemInstructions(chatflow, flowData, runtimeModelResults)));
        }

        if (knowledgeContext.SourceDocuments.Count > 0)
        {
            messages.Add(new ChatMessageContent(AuthorRole.System, BuildKnowledgeInstructions(knowledgeContext.SourceDocuments)));
        }

        if (toolContext.UsedTools.Count > 0)
        {
            messages.Add(new ChatMessageContent(AuthorRole.System, BuildAgentToolInstructions(toolContext.UsedTools)));
        }

        if (nodeDataReader.ReadNodeInputBool(node.Data, "agentEnableMemory"))
        {
            AddMemoryMessages(
                messages,
                nodeDataReader.ReadNodeInputString(node.Data, "agentMemoryType"),
                nodeDataReader.ReadNodeInputInt(node.Data, "agentMemoryWindowSize"),
                context);
        }

        var userMessage = templateResolver.ResolveAgentTemplate(
            nodeDataReader.ReadNodeInputString(node.Data, "agentUserMessage") ?? string.Empty,
            context,
            runtimeModelResults,
            httpResults,
            executeFlowResults,
            customFunctionResults,
            llmResults,
            agentResults);
        AppendUserMessage(messages, userMessage, context);
        return messages;
    }

    private void AddMemoryMessages(
        List<ChatMessageContent> messages,
        string? memoryType,
        int? memoryWindowSize,
        FlowiseExecutionContext context)
    {
        var resolvedMemoryType = memoryType ?? "allMessages";
        var history = context.ChatHistory
            .Where(item => !string.IsNullOrWhiteSpace(item.Content))
            .ToList();
        if (resolvedMemoryType.Equals("windowSize", StringComparison.OrdinalIgnoreCase))
        {
            var windowSize = Math.Clamp(memoryWindowSize ?? 20, 1, 100);
            history = history.TakeLast(windowSize * 2).ToList();
        }

        if (resolvedMemoryType.Contains("summary", StringComparison.OrdinalIgnoreCase) && history.Count > 0)
        {
            var summary = string.Join("\n", history.Select(item => $"{item.Role}: {item.Content}"));
            messages.Add(new ChatMessageContent(AuthorRole.System, $"Previous conversation summary: {summary}"));
            return;
        }

        foreach (var item in history)
        {
            messages.Add(new ChatMessageContent(executionContentParser.ToAuthorRole(item.Role), item.Content));
        }
    }

    private static void InsertByRole(List<ChatMessageContent> messages, AuthorRole role, string content)
    {
        if (role == AuthorRole.System)
        {
            messages.Insert(0, new ChatMessageContent(role, content));
            return;
        }

        messages.Add(new ChatMessageContent(role, content));
    }

    private static void AppendUserMessage(List<ChatMessageContent> messages, string userMessage, FlowiseExecutionContext context)
    {
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            messages.Add(new ChatMessageContent(AuthorRole.User, userMessage));
        }
        else if (!messages.Any(message => message.Role == AuthorRole.User))
        {
            messages.Add(new ChatMessageContent(AuthorRole.User, context.Question ?? string.Empty));
        }
    }

    private static string BuildKnowledgeInstructions(IReadOnlyList<FlowiseSourceDocumentDto> sourceDocuments)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Use the following retrieved document-store context when it is relevant. Cite only facts present in the context and say when the context is insufficient.");
        for (var index = 0; index < sourceDocuments.Count; index++)
        {
            var document = sourceDocuments[index];
            builder.AppendLine($"[Document {index + 1}] sourceId={document.SourceId}");
            builder.AppendLine(document.Content);
        }

        return builder.ToString().Trim();
    }

    private static string BuildAgentToolInstructions(IReadOnlyList<FlowiseUsedToolDto> usedTools)
    {
        var builder = new StringBuilder();
        builder.AppendLine("The following configured tools were executed before this response. Use their outputs as authoritative runtime evidence.");
        foreach (var tool in usedTools)
        {
            builder.AppendLine($"[Tool] {tool.Tool}");
            builder.AppendLine("Input:");
            builder.AppendLine(tool.InputJson);
            builder.AppendLine("Output:");
            builder.AppendLine(tool.OutputJson);
        }

        return builder.ToString().Trim();
    }

    private static string BuildSystemInstructions(
        FlowiseChatFlowEntity chatflow,
        FlowiseRuntimeFlowData flowData,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults)
    {
        var nodeNames = string.Join(", ", flowData.Nodes.Select(item => item.DisplayName).Where(item => !string.IsNullOrWhiteSpace(item)).Take(20));
        var prompt = $"You are executing Flowise workflow \"{chatflow.Name}\" of type {chatflow.Type}. Follow the configured graph semantics. Nodes: {nodeNames}.";
        if (runtimeModelResults.Count == 0)
        {
            return prompt;
        }

        return prompt + " Runtime model query results are authoritative business data; answer from them and do not invent missing rows.";
    }
}
