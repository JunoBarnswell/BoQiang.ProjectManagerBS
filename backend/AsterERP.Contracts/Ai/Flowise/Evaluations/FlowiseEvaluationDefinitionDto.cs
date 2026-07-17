namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseEvaluationDefinitionDto
{
    public string DatasetId { get; set; } = string.Empty;

    public string EvaluatorId { get; set; } = string.Empty;

    public string TargetFlowId { get; set; } = string.Empty;

    public string? Model { get; set; }

    public string RunConfigJson { get; set; } = "{}";
}
