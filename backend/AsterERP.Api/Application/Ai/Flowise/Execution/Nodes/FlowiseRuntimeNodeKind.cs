namespace AsterERP.Api.Application.Ai.Flowise;

internal enum FlowiseRuntimeNodeKind
{
    Start,
    Condition,
    RuntimeDataModel,
    Http,
    ExecuteFlow,
    CustomFunction,
    Llm,
    Agent,
    DirectReply,
    HumanInput,
    Iteration,
    Loop,
    Unsupported
}
