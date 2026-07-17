namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class FlowiseGraphNodeResults
{
    internal List<RuntimeDataModelNodeResult> RuntimeModel { get; } = [];
    internal List<HttpNodeResult> Http { get; } = [];
    internal List<ExecuteFlowNodeResult> ExecuteFlow { get; } = [];
    internal List<CustomFunctionNodeResult> CustomFunction { get; } = [];
    internal List<LlmNodeResult> Llm { get; } = [];
    internal List<AgentNodeResult> Agent { get; } = [];
}
