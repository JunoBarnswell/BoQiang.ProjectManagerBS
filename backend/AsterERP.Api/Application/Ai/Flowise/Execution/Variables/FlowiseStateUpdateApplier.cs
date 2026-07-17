using System.Text.Json;

namespace AsterERP.Api.Application.Ai.Flowise;

public sealed class FlowiseStateUpdateApplier(
    FlowiseKeyValueInputReader keyValueInputReader,
    FlowiseExecutionTemplateResolver templateResolver,
    FlowiseOutputReferenceResolver outputReferenceResolver,
    FlowiseVariableResolver variableResolver,
    FlowiseRuntimeNodeDataReader nodeDataReader)
{
    internal void ApplyLlmStateUpdate(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        string output,
        IReadOnlyDictionary<string, object?> structuredOutput)
    {
        foreach (var item in keyValueInputReader.Read(node.Data, "llmUpdateState", context, runtimeModelResults, httpResults))
        {
            var resolved = templateResolver.ResolveLlmTemplate(
                item.Value.Replace("{{output}}", output, StringComparison.OrdinalIgnoreCase),
                context,
                runtimeModelResults,
                httpResults,
                executeFlowResults,
                customFunctionResults,
                llmResults);
            foreach (var structured in structuredOutput)
            {
                resolved = resolved.Replace($"{{{{output.{structured.Key}}}}}", structured.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            context.SetFlowState(item.Key, resolved);
        }
    }

    internal void ApplyAgentStateUpdate(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        IReadOnlyList<LlmNodeResult> llmResults,
        IReadOnlyList<AgentNodeResult> agentResults,
        string output,
        IReadOnlyDictionary<string, object?> structuredOutput)
    {
        foreach (var item in keyValueInputReader.Read(node.Data, "agentUpdateState", context, runtimeModelResults, httpResults))
        {
            var resolved = templateResolver.ResolveAgentTemplate(
                item.Value.Replace("{{output}}", output, StringComparison.OrdinalIgnoreCase),
                context,
                runtimeModelResults,
                httpResults,
                executeFlowResults,
                customFunctionResults,
                llmResults,
                agentResults);
            foreach (var structured in structuredOutput)
            {
                resolved = resolved.Replace($"{{{{output.{structured.Key}}}}}", structured.Value?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
            }

            context.SetFlowState(item.Key, resolved);
        }
    }

    internal void ApplyExecuteFlowStateUpdate(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        string output)
    {
        foreach (var item in keyValueInputReader.Read(node.Data, "executeFlowUpdateState", context, runtimeModelResults, httpResults))
        {
            var resolved = outputReferenceResolver.ReplaceExecuteFlowOutputReferences(
                item.Value.Replace("{{output}}", output, StringComparison.OrdinalIgnoreCase),
                executeFlowResults);
            context.SetFlowState(item.Key, resolved);
        }
    }

    internal void ApplyCustomFunctionStateUpdate(
        FlowiseRuntimeNode node,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> runtimeModelResults,
        IReadOnlyList<HttpNodeResult> httpResults,
        IReadOnlyList<ExecuteFlowNodeResult> executeFlowResults,
        IReadOnlyList<CustomFunctionNodeResult> customFunctionResults,
        string output)
    {
        foreach (var item in keyValueInputReader.Read(node.Data, "customFunctionUpdateState", context, runtimeModelResults, httpResults))
        {
            var resolved = templateResolver.ResolveCustomFunctionTemplate(
                item.Value.Replace("{{output}}", output, StringComparison.OrdinalIgnoreCase),
                context,
                runtimeModelResults,
                httpResults,
                executeFlowResults,
                customFunctionResults);
            context.SetFlowState(item.Key, resolved);
        }
    }

    internal void ApplyLoopStateUpdate(
        FlowiseRuntimeNode loopNode,
        FlowiseExecutionContext context,
        IReadOnlyList<RuntimeDataModelNodeResult> previousResults,
        LoopOutput loopOutput)
    {
        if (!nodeDataReader.TryGetNodeInputValue(loopNode.Data, "loopUpdateState", out var stateUpdates) || stateUpdates.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var update in stateUpdates.EnumerateArray())
        {
            if (update.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var key = FlowiseJsonElementReader.ReadString(update, "key") ?? FlowiseJsonElementReader.ReadString(update, "Key");
            if (string.IsNullOrWhiteSpace(key))
            {
                continue;
            }

            var rawValue = FlowiseJsonElementReader.ReadString(update, "value") ?? FlowiseJsonElementReader.ReadString(update, "Value") ?? string.Empty;
            var resolved = variableResolver.ReplaceRuntimeVariables(rawValue, context, null, previousResults)
                .Replace("{{ output }}", loopOutput.Content, StringComparison.OrdinalIgnoreCase)
                .Replace("{{output}}", loopOutput.Content, StringComparison.OrdinalIgnoreCase);
            context.SetFlowState(key.Trim(), resolved);
        }
    }
}
