namespace AsterERP.Api.Application.Ai.Flowise;

internal sealed class CustomFunctionNodeResult
{
    public int ExecutionIndex { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string NodeLabel { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> InputVariables { get; set; } = new Dictionary<string, string>();

    public string Content { get; set; } = string.Empty;
}
