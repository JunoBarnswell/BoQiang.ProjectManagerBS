using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseExecutionTemplateResolver(
    FlowiseVariableResolver variableResolver,
    FlowiseOutputReferenceResolver outputReferenceResolver)
{
    internal string ResolveLlmTemplate(
        string value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults)
    {
        var resolved = ReplaceCustomFunctionFlowVariables(value, context);
        resolved = outputReferenceResolver.ReplaceHttpOutputReferences(
            variableResolver.ReplaceRuntimeVariables(resolved, context, null, runtimeModelResults),
            httpResults);
        resolved = outputReferenceResolver.ReplaceExecuteFlowOutputReferences(resolved, executeFlowResults);
        resolved = outputReferenceResolver.ReplaceCustomFunctionOutputReferences(resolved, customFunctionResults);
        return outputReferenceResolver.ReplaceLlmOutputReferences(resolved, llmResults);
    }

    internal string ResolveAgentTemplate(
        string value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults)
    {
        var resolved = ResolveLlmTemplate(
            value,
            context,
            runtimeModelResults,
            httpResults,
            executeFlowResults,
            customFunctionResults,
            llmResults);
        return outputReferenceResolver.ReplaceAgentOutputReferences(resolved, agentResults);
    }

    internal string ResolveHttpNodeInput(
        string? value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return outputReferenceResolver.ReplaceHttpOutputReferences(
            variableResolver.ReplaceRuntimeVariables(value, context, null, runtimeModelResults),
            httpResults);
    }

    internal string ResolveExecuteFlowInput(
        string? value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value ?? string.Empty;
        }

        return outputReferenceResolver.ReplaceExecuteFlowOutputReferences(
            outputReferenceResolver.ReplaceHttpOutputReferences(
                variableResolver.ReplaceRuntimeVariables(value, context, null, runtimeModelResults),
                httpResults),
            executeFlowResults);
    }

    internal string ResolveCustomFunctionTemplate(
        string? value,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyDictionary<string, string>? inputVariables = null)
    {
        var resolved = ReplaceCustomFunctionInputVariables(
            value ?? string.Empty,
            inputVariables ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
        resolved = ReplaceCustomFunctionFlowVariables(resolved, context);
        resolved = outputReferenceResolver.ReplaceHttpOutputReferences(
            variableResolver.ReplaceRuntimeVariables(resolved, context, null, runtimeModelResults),
            httpResults);
        resolved = outputReferenceResolver.ReplaceExecuteFlowOutputReferences(resolved, executeFlowResults);
        return outputReferenceResolver.ReplaceCustomFunctionOutputReferences(resolved, customFunctionResults);
    }

    private static string ReplaceCustomFunctionInputVariables(string value, IReadOnlyDictionary<string, string> inputVariables)
    {
        foreach (var item in inputVariables)
        {
            value = value.Replace($"${item.Key}", item.Value, StringComparison.OrdinalIgnoreCase);
            value = value.Replace($"{{{{${item.Key}}}}}", item.Value, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static string ReplaceCustomFunctionFlowVariables(string value, FlowiseExecutionContext context)
    {
        value = value.Replace("$flow.input", context.Question ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        value = value.Replace("$flow.chatflowId", context.ResourceId, StringComparison.OrdinalIgnoreCase);
        value = value.Replace("$flow.chatId", context.NormalizedChatId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        value = value.Replace("$flow.sessionId", context.NormalizedSessionId ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        foreach (var item in context.SnapshotFlowState())
        {
            value = value.Replace($"$flow.state.{item.Key}", item.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }
}
