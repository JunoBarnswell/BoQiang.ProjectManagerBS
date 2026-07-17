using System.Text.Json;
using AsterERP.Contracts.Ai;

namespace AsterERP.Api.Application.Ai.Tools.Workflow;

public abstract class AiWorkflowToolBase : IAiKernelFunction
{
    protected AiWorkflowToolBase(AiKernelFunctionDefinition definition)
    {
        Definition = definition;
    }

    public AiKernelFunctionDefinition Definition { get; }

    public abstract Task<AiKernelFunctionResult> ExecuteAsync(
        AiKernelFunctionContext context,
        CancellationToken cancellationToken);

    protected static AiKernelFunctionResult Result(
        string summary,
        object payload,
        string? evidenceJson = null,
        IReadOnlyList<AiKernelFunctionGeneratedEvent>? events = null)
    {
        return new AiKernelFunctionResult
        {
            ResultSummary = summary,
            Content = JsonSerializer.Serialize(payload, WorkflowJsonOptions.Options),
            EvidenceJson = evidenceJson ?? JsonSerializer.Serialize(payload, WorkflowJsonOptions.Options),
            OutputType = "Json",
            Events = events ?? []
        };
    }

    protected static string Evidence(params (string Key, object? Value)[] items) =>
        JsonSerializer.Serialize(items.ToDictionary(item => item.Key, item => item.Value), WorkflowJsonOptions.Options);

    protected static AiKernelFunctionGeneratedEvent Event(string eventName, string summary, object? payload) =>
        new(eventName, summary, payload);
}
