namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseEvaluatorDefinitionDto
{
    public string? Provider { get; set; }

    public string? Model { get; set; }

    public string? PromptTemplate { get; set; }

    public string? GradingMode { get; set; }

    public string AdvancedConfigJson { get; set; } = "{}";
}
