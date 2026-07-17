namespace AsterERP.Contracts.Ai.Flowise.Evaluations;

public sealed class FlowiseDatasetSchemaDto
{
    public IReadOnlyList<string> InputColumns { get; set; } = [];

    public IReadOnlyList<string> ExpectedOutputColumns { get; set; } = [];

    public string AdvancedSchemaJson { get; set; } = "{}";
}
